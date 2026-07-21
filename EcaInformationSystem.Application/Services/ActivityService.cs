using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs.Activity;

namespace EcaInformationSystem.Application.Services
{
    public class ActivityService : IActivityService
    {
        private readonly IActivityRepository _repo;
        private readonly IPsgcNameCache _psgcNameCache;

        public ActivityService(IActivityRepository repo, IPsgcNameCache psgcNameCache)
        {
            _repo = repo;
            _psgcNameCache = psgcNameCache;
        }

        public async Task<List<ActivityMonthMarkerDto>> GetMonthMarkersAsync(int year, int month, string? provinceCode, bool publicOnly = false)
        {
            var rangeStart = new DateTime(year, month, 1);
            var rangeEnd = rangeStart.AddMonths(1).AddDays(-1);

            var activities = await _repo.GetMonthRangeAsync(rangeStart, rangeEnd, provinceCode);
            if (publicOnly) activities = activities.Where(a => a.IsPublic).ToList();

            var map = new Dictionary<DateTime, ActivityMonthMarkerDto>();

            foreach (var a in activities.Where(a => !a.IsCancelled))
            {
                var actualStart = a.StartDate.Date;
                var actualEnd = (a.EndDate ?? a.StartDate).Date;
                var spanStart = actualStart < rangeStart ? rangeStart : actualStart;
                var spanEnd = actualEnd > rangeEnd ? rangeEnd : actualEnd;

                for (var d = spanStart; d <= spanEnd; d = d.AddDays(1))
                {
                    if (!map.TryGetValue(d, out var marker))
                    {
                        marker = new ActivityMonthMarkerDto { Date = d };
                        map[d] = marker;
                    }

                    marker.Priorities.Add(a.Priority);
                    marker.Segments.Add(new ActivitySpanSegmentDto
                    {
                        ActivityId = a.Id,
                        Title = a.Title,
                        Priority = a.Priority,
                        IsRangeStart = d == actualStart,
                        IsRangeEnd = d == actualEnd,
                        Location = a.Location,
                        Description = a.Description,
                        IsAllDay = a.IsAllDay,
                        StartDate = a.StartDate,
                        EndDate = a.EndDate,
                        IsPublic = a.IsPublic,
                        PsgcCodeRegion = a.PsgcCodeRegion,
                        RegionName = ResolveRegionName(a.PsgcCodeRegion)
                    });
                }
            }

            foreach (var m in map.Values) m.Count = m.Segments.Count;
            return map.Values.OrderBy(m => m.Date).ToList();
        }

        public async Task<List<ActivityDto>> GetActivitiesForDateAsync(DateTime date, string? provinceCode, bool publicOnly = false)
        {
            var activities = await _repo.GetByDateAsync(date, provinceCode);
            if (publicOnly) activities = activities.Where(a => a.IsPublic).ToList();
            return activities.Select(MapToDto).ToList();
        }

        public async Task<List<ActivityDto>> GetPublicUpcomingAsync(int take = 8)
        {
            var activities = await _repo.GetUpcomingPublicAsync(DateTime.Today, take);
            return activities.Select(MapToDto).ToList();
        }

        public async Task<ActivityDto?> GetByIdAsync(int id)
        {
            var a = await _repo.GetByIdAsync(id);
            return a is null ? null : MapToDto(a);
        }

        public async Task<ActivityDto> CreateAsync(ActivityUpsertDto dto, string userId, int regionCode)
        {
            var entity = new Activity
            {
                Title = dto.Title,
                Description = dto.Description,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                IsAllDay = dto.IsAllDay,
                Type = dto.Type,
                Priority = dto.Priority,
                Location = dto.Location,
                PsgcCodeProvince = dto.PsgcCodeProvince,
                PsgcCodeMunicipality = dto.PsgcCodeMunicipality,
                PsgcCodeRegion = regionCode,       // ✅ server-assigned
                IsPublic = dto.IsPublic,
                CreatedByUserId = userId,
                CreatedAt = DateTime.Now,
                ReminderSent = false
            };

            var created = await _repo.AddAsync(entity);
            return MapToDto(created);
        }

        public async Task<ActivityDto?> UpdateAsync(ActivityUpsertDto dto, string userId)
        {
            if (dto.Id is null) return null;
            var existing = await _repo.GetByIdAsync(dto.Id.Value);
            if (existing is null) return null;

            existing.Title = dto.Title;
            existing.Description = dto.Description;
            existing.StartDate = dto.StartDate;
            existing.EndDate = dto.EndDate;
            existing.IsAllDay = dto.IsAllDay;
            existing.Type = dto.Type;
            existing.Priority = dto.Priority;
            existing.Location = dto.Location;
            existing.PsgcCodeProvince = dto.PsgcCodeProvince;
            existing.PsgcCodeMunicipality = dto.PsgcCodeMunicipality;
            // PsgcCodeRegion never reassigned — an activity's owning region is permanent
            existing.IsPublic = dto.IsPublic;
            existing.UpdatedByUserId = userId;
            existing.UpdatedAt = DateTime.Now;
            existing.ReminderSent = false;

            var updated = await _repo.UpdateAsync(existing);
            return updated is null ? null : MapToDto(updated);
        }

        public Task<bool> DeleteAsync(int id) => _repo.DeleteAsync(id);

        private string? ResolveRegionName(int? regionCode)
        {
            if (!regionCode.HasValue) return null;
            return _psgcNameCache.GetRegionName(regionCode.Value);
        }

        private ActivityDto MapToDto(Activity a) => new()
        {
            Id = a.Id,
            Title = a.Title,
            Description = a.Description,
            StartDate = a.StartDate,
            EndDate = a.EndDate,
            IsAllDay = a.IsAllDay,
            Type = a.Type,
            Priority = a.Priority,
            Location = a.Location,
            PsgcCodeProvince = a.PsgcCodeProvince,
            PsgcCodeMunicipality = a.PsgcCodeMunicipality,
            IsCancelled = a.IsCancelled,
            IsPublic = a.IsPublic,
            PsgcCodeRegion = a.PsgcCodeRegion,
            RegionName = ResolveRegionName(a.PsgcCodeRegion)
        };
    }
}