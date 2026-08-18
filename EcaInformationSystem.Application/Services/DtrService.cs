using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs.Dtr;

namespace EcaInformationSystem.Application.Services
{
    // Derives a Civil Service Form No. 48 (Daily Time Record) from raw biometric
    // punches, for any caller-chosen date span (a full month, a semi-monthly
    // cutoff like 1-15, or any custom range). There's no stored work schedule
    // anywhere in the system, so the 8-hour/day requirement used for undertime
    // is a fixed standard-government workday assumption, not a per-employee
    // setting.
    public class DtrService : IDtrService
    {
        private const int RequiredMinutesPerDay = 480; // 8 hours

        private readonly IAttendanceLogRepository _attendanceLogRepository;
        private readonly IPendingUserRegistrationRepository _userRepository;

        public DtrService(IAttendanceLogRepository attendanceLogRepository, IPendingUserRegistrationRepository userRepository)
        {
            _attendanceLogRepository = attendanceLogRepository;
            _userRepository = userRepository;
        }

        public async Task<DtrDocumentDto?> GetForUserAsync(Guid userId, DateTime periodStart, DateTime periodEnd)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user is null || string.IsNullOrWhiteSpace(user.BiometricUserId))
                return null;

            periodStart = periodStart.Date;
            periodEnd = periodEnd.Date;

            var logs = await _attendanceLogRepository.GetByUserAsync(user.BiometricUserId, periodStart, periodEnd.AddDays(1).AddTicks(-1));
            var punchesByDay = logs.GroupBy(l => l.PunchTime.Date).ToDictionary(
                g => g.Key,
                g => g.Select(l => (Time: l.PunchTime, l.Id)).OrderBy(t => t.Time).ToList());

            var doc = new DtrDocumentDto
            {
                UserId = user.Id,
                EmployeeName = user.FullName,
                Position = user.Position,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                ForTheMonthLabel = FormatPeriodLabel(periodStart, periodEnd)
            };

            // The form always shows the whole month (matching Civil Service Form
            // No. 48's fixed 1-31 layout) even when only part of it was
            // requested — days outside [periodStart, periodEnd] still get a
            // row, just with IsInPeriod=false so the print view draws a
            // diagonal slash instead of AM/PM columns. "The month" is taken
            // from periodStart, since the semi-monthly presets (1-15, 16-end)
            // and typical custom ranges never cross a month boundary.
            var monthStart = new DateTime(periodStart.Year, periodStart.Month, 1);
            var daysInMonth = DateTime.DaysInMonth(periodStart.Year, periodStart.Month);
            var monthEnd = new DateTime(periodStart.Year, periodStart.Month, daysInMonth);

            for (var date = monthStart; date <= monthEnd; date = date.AddDays(1))
            {
                var isInPeriod = date >= periodStart && date <= periodEnd;

                var dayType = date.DayOfWeek switch
                {
                    DayOfWeek.Saturday => DtrDayType.Saturday,
                    DayOfWeek.Sunday => DtrDayType.Sunday,
                    _ => DtrDayType.Workday
                };

                var punches = isInPeriod && punchesByDay.TryGetValue(date, out var p) ? p : [];
                var computed = ComputeDay(punches);

                doc.Days.Add(new DtrDayDto
                {
                    Day = date.Day,
                    DayType = dayType,
                    IsInPeriod = isInPeriod,
                    AmTimeIn = isInPeriod ? computed.AmIn : null,
                    AmTimeOut = isInPeriod ? computed.AmOut : null,
                    PmTimeIn = isInPeriod ? computed.PmIn : null,
                    PmTimeOut = isInPeriod ? computed.PmOut : null,
                    AmTimeInLogId = isInPeriod ? computed.AmInId : null,
                    AmTimeOutLogId = isInPeriod ? computed.AmOutId : null,
                    PmTimeInLogId = isInPeriod ? computed.PmInId : null,
                    PmTimeOutLogId = isInPeriod ? computed.PmOutId : null,
                    UndertimeHours = isInPeriod && dayType == DtrDayType.Workday ? computed.UndertimeHours : 0,
                    UndertimeMinutes = isInPeriod && dayType == DtrDayType.Workday ? computed.UndertimeMinutes : 0
                });
            }

            var totalMinutes = doc.Days.Sum(d => d.UndertimeHours * 60 + d.UndertimeMinutes);
            doc.TotalUndertimeHours = totalMinutes / 60;
            doc.TotalUndertimeMinutes = totalMinutes % 60;

            return doc;
        }

        public async Task<List<DtrSummaryDto>> GetAllSummariesAsync(DateTime periodStart, DateTime periodEnd, int regionCode)
        {
            var users = await _userRepository.GetAllAsync();
            periodStart = periodStart.Date;
            periodEnd = periodEnd.Date.AddDays(1).AddTicks(-1);

            var summaries = new List<DtrSummaryDto>();
            foreach (var user in users.Where(u => u.Region == regionCode && !string.IsNullOrWhiteSpace(u.BiometricUserId)))
            {
                var logs = await _attendanceLogRepository.GetByUserAsync(user.BiometricUserId!, periodStart, periodEnd);
                var daysWithPunches = logs.Select(l => l.PunchTime.Date).Distinct().Count();

                var punchesByDay = logs.GroupBy(l => l.PunchTime.Date).ToDictionary(
                    g => g.Key,
                    g => g.Select(l => (Time: l.PunchTime, l.Id)).OrderBy(t => t.Time).ToList());
                var totalUndertimeMinutes = 0;
                foreach (var (date, punches) in punchesByDay)
                {
                    if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                        continue;

                    var computed = ComputeDay(punches);
                    totalUndertimeMinutes += computed.UndertimeHours * 60 + computed.UndertimeMinutes;
                }

                summaries.Add(new DtrSummaryDto
                {
                    UserId = user.Id,
                    EmployeeName = user.FullName,
                    Position = user.Position,
                    BiometricUserId = user.BiometricUserId,
                    DaysWithPunches = daysWithPunches,
                    TotalUndertimeHours = totalUndertimeMinutes / 60,
                    TotalUndertimeMinutes = totalUndertimeMinutes % 60
                });
            }

            return summaries.OrderBy(s => s.EmployeeName).ToList();
        }

        private static string FormatPeriodLabel(DateTime start, DateTime end)
        {
            if (start.Year == end.Year && start.Month == end.Month)
                return $"{start:MMMM d}-{end.Day}, {end:yyyy}";

            if (start.Year == end.Year)
                return $"{start:MMMM d} - {end:MMMM d}, {end:yyyy}";

            return $"{start:MMMM d, yyyy} - {end:MMMM d, yyyy}";
        }

        private sealed record DayComputation(
            string? AmIn, string? AmOut, string? PmIn, string? PmOut,
            int? AmInId, int? AmOutId, int? PmInId, int? PmOutId,
            int UndertimeHours, int UndertimeMinutes);

        // Splits a day's raw punches into AM in/out and PM in/out following the
        // standard 2-punch (in/out only) or 4-punch (in/lunch-out/lunch-in/out)
        // patterns. Every slot's AttendanceLog Id flows through regardless of
        // whether it was a real device scan or a manual entry — self-service
        // editing needs to reference (and clear/replace) either kind — a
        // manual punch just sits in the same chronological sequence and gets
        // sorted into whichever slot its position implies, same as any device
        // punch would.
        private static DayComputation ComputeDay(List<(DateTime Time, int Id)> sortedPunches)
        {
            if (sortedPunches.Count == 0)
                return new DayComputation(null, null, null, null, null, null, null, null, 0, 0);

            // Collapse near-duplicate scans (an accidental double-tap on the
            // device) — without this, a second scan seconds after the first
            // gets misread as a separate lunch punch.
            var punches = new List<(DateTime Time, int Id)> { sortedPunches[0] };
            foreach (var p in sortedPunches.Skip(1))
            {
                if ((p.Time - punches[^1].Time).TotalMinutes >= 2)
                    punches.Add(p);
            }

            // Raw instants behind amIn/amOut/pmIn/pmOut below — kept alongside
            // the formatted strings so undertime is computed from exactly the
            // same punches the UI displays, never independently re-derived
            // from punches.Count/position (see bug note below).
            var amInTime = punches[0].Time;
            DateTime? amOutTime = null;
            DateTime? pmInTime = null;
            DateTime? pmOutTime = null;

            string? amIn = Format(amInTime);
            string? amOut = null;
            string? pmIn = null;
            string? pmOut = null;
            int? amInId = punches[0].Id;
            int? amOutId = null;
            int? pmInId = null;
            int? pmOutId = null;

            switch (punches.Count)
            {
                case 1:
                    break; // just clocked in so far
                case 2:
                    // No lunch punches logged — a plain whole-day in/out.
                    pmOutTime = punches[1].Time;
                    pmOut = Format(pmOutTime.Value);
                    pmOutId = punches[1].Id;
                    break;
                case 3:
                    // This office's convention: morning-in, lunch-out,
                    // lunch-in — strictly by POSITION, not clock hour (lunch
                    // punches routinely land in the 12:xx PM hour on both
                    // sides). The day isn't over yet, so PM Time Out stays
                    // blank rather than being guessed from the lunch-in
                    // punch — it only gets filled by an actual 4th scan.
                    amOutTime = punches[1].Time;
                    pmInTime = punches[2].Time;
                    amOut = Format(amOutTime.Value);
                    pmIn = Format(pmInTime.Value);
                    amOutId = punches[1].Id;
                    pmInId = punches[2].Id;
                    break;
                default:
                    // 4+ punches: in, lunch-out, lunch-in, ..., out. Any
                    // punches strictly between index 1 and ^2 (a 5th+ stray
                    // scan) are neither shown nor counted — they'd otherwise
                    // silently corrupt the undertime sum below without ever
                    // appearing anywhere in the UI to explain why.
                    amOutTime = punches[1].Time;
                    pmInTime = punches[^2].Time;
                    pmOutTime = punches[^1].Time;
                    amOut = Format(amOutTime.Value);
                    pmIn = Format(pmInTime.Value);
                    pmOut = Format(pmOutTime.Value);
                    amOutId = punches[1].Id;
                    pmInId = punches[^2].Id;
                    pmOutId = punches[^1].Id;
                    break;
            }

            // Undertime only makes sense once the day actually has an end —
            // a day with only an AM-in (1 punch) or AM-in/AM-out/PM-in with
            // no clock-out yet (3 punches) isn't over, so Hrs/Min stay blank
            // rather than showing a misleadingly large "undertime" measured
            // against a day that hasn't finished.
            if (pmOutTime is null)
                return new DayComputation(amIn, amOut, pmIn, pmOut, amInId, amOutId, pmInId, pmOutId, 0, 0);

            // Sum ONLY the AM and PM stretches actually shown (in the 2-punch
            // case, that's the whole day as one stretch, amOutTime/pmInTime
            // both null) — never a generic positional pairing over every raw
            // punch, which broke for 5+ punch days (undertime silently summed
            // different punches than the ones on screen).
            var workedMinutes = amOutTime.HasValue && pmInTime.HasValue
                ? (amOutTime.Value - amInTime).TotalMinutes + (pmOutTime.Value - pmInTime.Value).TotalMinutes
                : (pmOutTime.Value - amInTime).TotalMinutes;

            var undertimeMinutes = Math.Max(0, RequiredMinutesPerDay - (int)workedMinutes);
            return new DayComputation(amIn, amOut, pmIn, pmOut, amInId, amOutId, pmInId, pmOutId, undertimeMinutes / 60, undertimeMinutes % 60);
        }

        private static string Format(DateTime t) => t.ToString("h:mm tt");
    }
}
