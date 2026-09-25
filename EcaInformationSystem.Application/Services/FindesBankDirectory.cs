using System.Text;

namespace EcaInformationSystem.Application.Services
{
    // ── Bank / EMI reference table for the FINDES & WEACCESS fund-transfer
    // upload file ─────────────────────────────────────────────────────────
    // The upload file's first two columns are looked up by BANK NAME: the
    // grantee's stored bank (Annex A Section E "Bank or Wallet Name", free
    // text, typed by hand) has to be resolved to
    //   • Bank Code    — the "BANK/EMI CODE" column of the Bank/Electronic
    //                    Money Issuer Name list (Annex A)
    //   • BICFI        — the BIC column of that same list
    // Since the stored name is free text ("LANDBANK", "Land Bank", "LBP"),
    // resolution is a fuzzy match: exact alias hit first, then a similarity
    // score (same Levenshtein approach already used for name searches in
    // BeneficiaryInformationRepository), and a containment check so a stored
    // value like "BDO UNIBANK INC" still hits the "BDO" entry.
    //
    // Entries is the Annex A table copied verbatim from
    // "ANNEX A — List of PESONet Participants" (Philippine Clearing House
    // Corporation): BANK/EMI CODE, BIC, BANK/ELECTRONIC MONEY ISSUER NAME.
    // Codes and BICs are transcribed as printed — never approximated, since a
    // wrong code makes the whole upload file unusable. Anything that doesn't
    // match a row here gets a blank Bank Code rather than a guess — Palawan
    // Pawnshop (channel 4) included, since the PSP is not a PESONet
    // participant and so has no row.
    //
    // Aliases cover how the same institution actually gets typed into the
    // grantee record's free-text "Bank Name" field (Annex A Section E.2):
    // acronyms the field note asks staff not to use but that appear anyway,
    // and the full spellings of names Annex A itself abbreviates
    // ("PHILS." → PHILIPPINES, "INT'L" → INTERNATIONAL).
    public static class FindesBankDirectory
    {
        // IsMobileWallet: the issuer pays out to a mobile number rather than to
        // a bank account — Annex A's electronic money issuer rows (GCash, Maya,
        // PayMaya, Coins.ph, JuanCash, TayoCash). Such a record can be filed
        // under "Other Banks" (channel 2) instead of the EMI channel, and its
        // number still goes out in the file's '00000 + 11-digit form — see
        // BuildCreditorAcctNum in StatisticsService.
        public sealed record FindesBankEntry(
            string EmiCode,
            string Bic,
            string Name,
            string[]? Aliases = null,
            bool IsMobileWallet = false);

        public static readonly IReadOnlyList<FindesBankEntry> Entries = new List<FindesBankEntry>
        {
            new("036", "AIIPPHM1XXX", "AL-AMANAH ISLAMIC INVEST. BANK", new[] { "AL AMANAH ISLAMIC INVESTMENT BANK", "AL-AMANAH" }),
            new("129", "ALKBPHM2XXX", "ALLBANK, INC.", new[] { "ALLBANK" }),
            new("102", "AUBKPHMHXXX", "ASIA UNITED BANK", new[] { "ASIA UNITED BANK CORPORATION", "AUB" }),
            new("070", "ANZBPHMXXXX", "AUSTRALIA-NEW ZEALAND BANK", new[] { "ANZ BANK", "ANZ" }),
            new("053", "BNORPHMHXXX", "BANCO DE ORO UNIBANK, INC.", new[] { "BANCO DE ORO", "BANCO DE ORO UNIBANK", "BDO", "BDO UNIBANK" }),
            new("160", "KARUPHM1XXX", "BANGKO KABAYAN (PRIVATE DEV'T. BANK)", new[] { "BANGKO KABAYAN PRIVATE DEVELOPMENT BANK", "BANGKO KABAYAN" }),
            new("907", "MRTCPHM1XXX", "BANGKO MABUHAY (A RURAL BANK), INC.", new[] { "BANGKO MABUHAY" }),
            new("137", "NSPRPHM1XXX", "BANGKO NUESTRA SEÑORA DEL PILAR, INC.", new[] { "BANGKO NUESTRA SENORA DEL PILAR" }),
            new("067", "BKKBPHMHXXX", "BANGKOK BANK PUBLIC CO., LTD", new[] { "BANGKOK BANK" }),
            new("012", "BOFAPH2XXXX", "BANK OF AMERICA, NAT'L. ASSN.", new[] { "BANK OF AMERICA" }),
            new("114", "BKCHPHMHXXX", "BANK OF CHINA (HONG KONG), LIMITED", new[] { "BANK OF CHINA" }),
            new("044", "PABIPHMHXXX", "BANK OF COMMERCE"),
            new("121", "MKRUPHM1XXX", "BANK OF MAKATI (A SAVINGS BANK), INC.", new[] { "BANK OF MAKATI" }),
            new("004", "BOPIPHMHXXX", "BANK OF THE PHILIPPINE ISLANDS", new[] { "BPI" }),
            new("054", "BPDIPHM1XXX", "BANCO, A SUBSIDIARY OF BPI", new[] { "BPI DIRECT BANKO" }),
            new("118", "ONNRPHM1XXX", "BDO NETWORK BANK", new[] { "BDO NETWORK" }),
            new("183", "BIURPHM2XXX", "BIÑAN RURAL BANK, INC.", new[] { "BINAN RURAL BANK" }),
            new("140", "BORRPHM1XXX", "BOF, INC (A RURAL BANK)"),
            new("146", "RUCAPHM1XXX", "CAMALIG BANK, INC. (A RURAL BANK)", new[] { "CAMALIG BANK" }),
            new("149", "CNRLPHM1XXX", "CANTILAN BANK, INC.", new[] { "CANTILAN BANK" }),
            new("132", "UWCBPHMHXXX", "CATHAY UNITED BANK CO., LTD.", new[] { "CATHAY UNITED BANK" }),
            new("144", "CELRPHM1XXX", "CEBUANA LHUILLIER BANK (A RURAL BANK)", new[] { "CEBUANA LHUILLIER BANK", "CEBUANA" }),
            new("112", "CHSVPHM1XXX", "CHINA BANK SAVINGS, INC.", new[] { "CHINA BANK SAVINGS" }),
            new("010", "CHBKPHMHXXX", "CHINA BANKING CORPORATION", new[] { "CHINA BANK", "CHINABANK" }),
            new("801", "CIPHPHM1XXX", "CIMB BANK", new[] { "CIMB BANK PHILIPPINES", "CIMB" }),
            new("007", "CITIPHMXXXX", "CITIBANK, N. A.", new[] { "CITIBANK" }),
            new("152", "CUOBPHM2XXX", "COMMUNITY RURAL BANK OF ROMBLON, INC."),
            new("180", "CBQPPHM2XXX", "COOPERATIVE BANK OF QUEZON PROVINCE"),
            new("145", "COUKPHM1XXX", "COUNTRY BUILDERS BANK, INC. (A RURAL BANK)"),
            new("069", "CTCBPHMHXXX", "CTBC BANK (PHILIPPINES) CORP.", new[] { "CTBC BANK" }),
            new("803", "DCPPHM1XXX", "DC PAY (COINS.PH)", new[] { "COINS PH", "COINSPH", "DC PAY" }, IsMobileWallet: true),
            new("065", "DEUTPHMHXXX", "DEUTSCHE BANK"),
            new("059", "DBPHPHMHXXX", "DEVELOPMENT BANK OF THE PHILS.", new[] { "DEVELOPMENT BANK OF THE PHILIPPINES", "DBP", "DEV BANK" }),
            new("153", "DCDEPHM1XXX", "DUMAGUETE CITY DEVELOPMENT BANK, INC."),
            new("902", "DUMTPHM1XXX", "DUNGGANON BANK, INC."),
            new("905", "EAWRPHM2XXX", "EAST WEST RURAL BANK, INC."),
            new("082", "EWBCPHMHXXX", "EAST-WEST BANKING CORPORATION", new[] { "EAST WEST BANK", "EAST WEST", "EASTWEST" }),
            new("096", "EQSNPHM1XXX", "EQUICOM SAVINGS BANK, INC.", new[] { "EQUICOM SAVINGS BANK" }),
            new("078", "FIOOPHM1XXX", "FIRST CONSOLIDATED BANK"),
            new("804", "GXCHPHM2XXX", "GLOBE XCHANGE, INC. (GCASH)", new[] { "GCASH", "G-XCHANGE", "G XCHANGE", "GLOBE XCHANGE" }, IsMobileWallet: true),
            new("139", "GRBUPHM1XXX", "GUAGUA RURAL BANK, INC."),
            new("006", "HSBCPHMHXXX", "HK AND SHANGHAI BANKING CORP", new[] { "HSBC", "HONGKONG AND SHANGHAI BANKING CORPORATION", "HONG KONG AND SHANGHAI BANKING CORP" }),
            new("089", "HBPHPHMHXXX", "HSBC SAVINGS BANK PHILS, INC.", new[] { "HSBC SAVINGS BANK PHILIPPINES", "HSBC SAVINGS BANK" }),
            new("147", "ICBKPHMHXXX", "INDUSTRIAL AND COMMERCIAL BANK OF CHINA, LT", new[] { "INDUSTRIAL AND COMMERCIAL BANK OF CHINA" }),
            new("131", "IBKOPHMHXXX", "INDUSTRIAL BANK OF KOREA"),
            new("066", "INGBPHMMRTL", "ING BANK N.V.", new[] { "ING BANK" }),
            new("906", "IORUPHM1XXX", "INNOVATIVE BANK, INC. (A RURAL BANK)"),
            new("072", "CHASPHMHXXX", "JPMORGAN CHASE BANK", new[] { "JPMORGAN CHASE", "JP MORGAN CHASE" }),
            new("071", "KOEXPHM1XXX", "KEB HANA BANK"),
            new("174", "LPCRPHM2XXX", "LAGUNA PRESTIGE BANKING CORPORATION"),
            new("035", "TLBPPHM1XXX", "LAND BANK OF THE PHILIPPINES", new[] { "LANDBANK", "LBP", "LAND BANK", "LANDBANK OF THE PHILIPPINES" }),
            new("815", "LFSHPHM2XXX", "LULU FINANCIAL SERVICES (PHILS), INC.", new[] { "LULU FINANCIAL SERVICES" }),
            new("181", "MLRUPHM2XXX", "MALARAYAT RURAL BANK, INC."),
            new("082", "MAARPHM1XXX", "MALAYAN SAVINGS BANK, INC.", new[] { "MALAYAN SAVINGS BANK" }),
            new("187", "MYYAPHM2XXX", "MAYA BANK, INC.", new[] { "MAYA BANK", "MAYA", "MAYA PHILIPPINES", "MAYA WALLET" }, IsMobileWallet: true),
            new("022", "MBBEPHMHXXX", "MAYBANK PHILS., INC.", new[] { "MAYBANK PHILIPPINES", "MAYBANK" }),
            new("056", "ICBCPHMHXXX", "MEGA INT'L. COMM'L BANK CO. LTD", new[] { "MEGA INTERNATIONAL COMMERCIAL BANK" }),
            new("026", "MBTCPHMHXXX", "METROPOLITAN BANK AND TRUST CO.", new[] { "METROBANK", "METROPOLITAN BANK AND TRUST COMPANY" }),
            new("064", "MHCBPHMHXXX", "MIZUHO BANK, LTD.", new[] { "MIZUHO BANK" }),
            new("156", "MOMILPHM2XXX", "MONEY MALL RURAL BANK, INC."),
            new("046", "BOTKPHMHXXX", "MUFG BANK, LTD.", new[] { "MUFG BANK" }),
            new("159", "MVRSPHM2XXX", "MVSM BANK (A RURAL BANK), INC."),
            new("802", "PAPHPHM1XXX", "PAYMAYA PHILIPPINES, INC.", new[] { "PAYMAYA", "PAY MAYA" }, IsMobileWallet: true),
            new("011", "CPHIPHMHXXX", "PHILIPPINE BANK OF COMMUNICATIONS", new[] { "PBCOM" }),
            new("097", "PPBUPHMHXXX", "PHILIPPINE BUSINESS BANK", new[] { "PBB" }),
            new("008", "PNBMPHMHXXX", "PHILIPPINE NATIONAL BANK", new[] { "PNB" }),
            new("047", "PHSBPHMHXXX", "PHILIPPINE SAVINGS BANK", new[] { "PSBANK", "PS BANK" }),
            new("009", "PHTBPHMHXXX", "PHILIPPINE TRUST COMPANY", new[] { "PHILTRUST" }),
            new("033", "PHVBPHMHXXX", "PHILIPPINE VETERANS BANK", new[] { "PVB" }),
            new("122", "PSCOPHMHXXX", "PRODUCERS SAVINGS BANK CORP.", new[] { "PRODUCERS SAVINGS BANK", "PRODUCERS BANK" }),
            new("023", "QCDPPHM1XXX", "QUEEN CITY DEVELOPMENT BANK"),
            new("908", "RARLPHM1XXX", "RANG-AY BANK, INC.", new[] { "RANG AY BANK" }),
            new("169", "RBRUPHM2XXX", "RBT BANK, INC. (A RURAL BANK)"),
            new("028", "RCBCPHMHXXX", "RIZAL COMMERCIAL BANKING CORP.", new[] { "RCBC", "RIZAL COMMERCIAL BANKING CORPORATION" }),
            new("107", "ROBPPHM2XXX", "ROBINSONS BANK CORPORATION", new[] { "ROBINSONS BANK" }),
            new("166", "RUBCPHM2XXX", "RURAL BANK OF BACOLOD CITY, INC."),
            new("178", "RUBUPHM2XXX", "RURAL BANK OF BAUANG, INC."),
            new("151", "RUDIPHM1XXX", "RURAL BANK OF DIGOS, INC."),
            new("148", "RUGUPHM1XXX", "RURAL BANK OF GUINOBATAN, INC."),
            new("176", "RUPZPHM2XXX", "RURAL BANK OF LA PAZ, INC."),
            new("177", "RLSKPHM1XXX", "RURAL BANK OF LEBAK (SULTAN KUDARAT), INC."),
            new("179", "RUMTPHM2XXX", "RURAL BANK OF MONTALBAN, INC."),
            new("158", "RUPPHM2XXX", "RURAL BANK OF PORAC (PAMPANGA), INC."),
            new("168", "RURUPHM2XXX", "RURAL BANK OF ROSARIO (LA UNION), INC."),
            new("171", "RUSYPHM2XXX", "RURAL BANK OF SAGAY, INC."),
            new("175", "RUSGPHM1XXX", "RURAL BANK OF STA. IGNACIA, INC. (SIGNA BANK)", new[] { "SIGNA BANK", "RURAL BANK OF SANTA IGNACIA" }),
            new("182", "LAUIPHM2XXX", "SEABANK PHILIPPINES, INC. (A RURAL BANK)", new[] { "SEABANK", "SEA BANK PHILIPPINES" }),
            new("014", "SETCPHMHXXX", "SECURITY BANK CORPORATION", new[] { "SECURITY BANK" }),
            new("130", "SHBKPHMHXXX", "SHINHAN BANK"),
            new("119", "STLAPH22XXX", "STERLING BANK OF ASIA, INC.", new[] { "STERLING BANK OF ASIA" }),
            new("128", "SMBCPHMHXXX", "SUMITOMO MITSUI BANKING CORP.", new[] { "SUMITOMO MITSUI BANKING CORPORATION" }),
            new("809", "TAYOPHM2XXX", "TAYOCASH INC.", new[] { "TAYO CASH", "TAYOCASH" }, IsMobileWallet: true),
            new("005", "SCBLPHMHXXX", "THE STANDARD CHARTERED BANK", new[] { "STANDARD CHARTERED BANK", "STANDARD CHARTERED" }),
            new("157", "TODGPHM2XXX", "TONIK DIGITAL BANK, INC.", new[] { "TONIK DIGITAL BANK", "TONIK" }),
            new("041", "UBPHPHMHXXX", "UNION BANK OF THE PHILS.", new[] { "UNION BANK OF THE PHILIPPINES", "UNIONBANK", "UNION BANK" }),
            new("029", "UCPBPHMHXXX", "UNITED COCONUT PLANTERS BANK"),
            new("027", "UOVBPHMHXXX", "UNITED OVERSEAS BANK PHILS.", new[] { "UNITED OVERSEAS BANK PHILIPPINES", "UOB" }),
            new("811", "USIMEPHM2XXX", "USSC MONEY SERVICES, INC.", new[] { "USSC MONEY SERVICES", "USSC" }),
            new("120", "WEDVPHM1XXX", "WEALTH DEVELOPMENT BANK, CORP.", new[] { "WEALTH DEVELOPMENT BANK" }),
            new("113", "TYBKPHMHXXX", "YUANTA SAVINGS BANK, INC.", new[] { "YUANTA SAVINGS BANK" }),
            new("808", "ZBTEPHM2XXX", "ZYBI TECH, INC. (JUANCASH)", new[] { "JUANCASH", "ZYBI TECH" }, IsMobileWallet: true)
        };

        // Section E.1's first option *is* "Landbank of the Philippines", so a
        // record on that channel with no Bank Name typed in still resolves —
        // see ExportFindesUploadAsync.
        public static FindesBankEntry? Landbank =>
            Entries.FirstOrDefault(e => e.EmiCode == "035");

        // Words dropped before comparing — only ones that never distinguish
        // two Annex A entries from each other. Deliberately NOT included:
        // BANK, BANKING, SAVINGS, RURAL, THRIFT and the PHILIPPINES/PHILS
        // family. They look like filler, but in the real list they carry the
        // difference between entries — dropping them collapsed
        // "EAST-WEST BANKING CORPORATION" and "EAST WEST RURAL BANK" onto the
        // same key, "PHILIPPINE SAVINGS BANK" onto nothing at all, and
        // "LAND BANK OF THE PHILIPPINES" onto a bare "LAND".
        // The PHILS./PHILIPPINES spelling difference it used to smooth over is
        // handled by aliases on those entries instead.
        private static readonly HashSet<string> NoiseWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "THE", "OF", "AND", "A", "AN",
            "INC", "INCORPORATED", "CORP", "CORPORATION", "CO", "COMPANY",
            "LTD", "LIMITED", "GROUP", "HOLDINGS", "SERVICES", "BRANCH"
        };

        // Below this similarity, a name is treated as "no match" rather than
        // forcing the nearest bank onto the row.
        private const double MatchThreshold = 0.86;

        // Lazy so the alias index is built once, the first time an export runs.
        private static readonly Lazy<Dictionary<string, FindesBankEntry>> ExactIndex =
            new(BuildExactIndex, isThreadSafe: true);

        public static bool TryResolve(string? storedBankName, out FindesBankEntry entry)
        {
            entry = default!;
            if (string.IsNullOrWhiteSpace(storedBankName))
                return false;

            var normalized = Normalize(storedBankName);
            if (normalized.Length == 0)
                return false;

            // 1) Exact hit on the full name or any alias — the common case once
            //    the list is filled in, since most stored values are typed the
            //    same way as the list.
            if (ExactIndex.Value.TryGetValue(normalized, out var exact) && exact is not null)
            {
                entry = exact;
                return true;
            }

            // 2) Fuzzy: best similarity across every name/alias, with a
            //    containment bonus so "BDO UNIBANK" matches "BDO".
            FindesBankEntry? best = null;
            var bestScore = 0.0;

            foreach (var candidate in Entries)
            {
                var score = ScoreAgainstEntry(normalized, candidate);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            if (best is null || bestScore < MatchThreshold)
                return false;

            entry = best;
            return true;
        }

        private static double ScoreAgainstEntry(string normalizedStored, FindesBankEntry candidate)
        {
            var best = 0.0;

            foreach (var name in CandidateNames(candidate))
            {
                var normalizedCandidate = Normalize(name);
                if (normalizedCandidate.Length == 0)
                    continue;

                best = Math.Max(best, Similarity(normalizedStored, normalizedCandidate));

                // Containment: "BDO UNIBANK" vs "BDO", "GCASH" vs "GCASH
                // (G-XCHANGE)". Requires a reasonably long shorter side so a
                // 2-3 letter alias can't swallow every bank that contains it.
                var shorter = Math.Min(normalizedStored.Length, normalizedCandidate.Length);
                if (shorter >= 4 &&
                    (normalizedStored.Contains(normalizedCandidate, StringComparison.Ordinal) ||
                     normalizedCandidate.Contains(normalizedStored, StringComparison.Ordinal)))
                {
                    best = Math.Max(best, 0.95);
                }
            }

            return best;
        }

        private static IEnumerable<string> CandidateNames(FindesBankEntry entry)
        {
            yield return entry.Name;

            if (entry.Aliases is null)
                yield break;

            foreach (var alias in entry.Aliases)
                yield return alias;
        }

        private static Dictionary<string, FindesBankEntry> BuildExactIndex()
        {
            var index = new Dictionary<string, FindesBankEntry>(StringComparer.Ordinal);

            foreach (var entry in Entries)
            {
                foreach (var name in CandidateNames(entry))
                {
                    var normalized = Normalize(name);
                    if (normalized.Length > 0)
                        index[normalized] = entry;
                }
            }

            return index;
        }

        // Uppercase, drop punctuation, drop noise words, collapse spaces —
        // so "BPI (Bank of the Philippine Islands), Inc." and "BANK OF THE
        // PHILIPPINE ISLANDS" normalize to comparable strings.
        internal static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var builder = new StringBuilder(value.Length);
            foreach (var ch in value)
            {
                if (char.IsLetterOrDigit(ch))
                    builder.Append(char.ToUpperInvariant(ch));
                else
                    builder.Append(' ');
            }

            var tokens = builder.ToString()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(t => !NoiseWords.Contains(t))
                .ToArray();

            return string.Join(' ', tokens);
        }

        private static double Similarity(string a, string b)
        {
            if (a.Length == 0 || b.Length == 0) return 0.0;
            if (a == b) return 1.0;

            var distance = LevenshteinDistance(a, b);
            return 1.0 - (double)distance / Math.Max(a.Length, b.Length);
        }

        private static int LevenshteinDistance(string a, string b)
        {
            var prev = new int[b.Length + 1];
            var curr = new int[b.Length + 1];

            for (var j = 0; j <= b.Length; j++)
                prev[j] = j;

            for (var i = 1; i <= a.Length; i++)
            {
                curr[0] = i;

                for (var j = 1; j <= b.Length; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    curr[j] = Math.Min(
                        Math.Min(curr[j - 1] + 1, prev[j] + 1),
                        prev[j - 1] + cost);
                }

                (prev, curr) = (curr, prev);
            }

            return prev[b.Length];
        }

        // ── Phone helpers for the transfer file ────────────────────────────
        // TelMobile is the grantee's own number; CreditorAcctNum uses the
        // preferred payment's number prefixed, for payouts that go out through
        // a mobile wallet (or Palawan Pawnshop) instead of a real account
        // number.

        // Digits only, in the 11-digit 09XXXXXXXXX form. Stored numbers come
        // through as "+639171234567", "09171234567", "9171234567" or with
        // spaces/dashes, so normalize rather than reject.
        public static bool TryNormalizePhMobile(string? value, out string digits)
        {
            digits = string.Empty;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var raw = new string(value.Where(char.IsDigit).ToArray());
            if (raw.Length == 0)
                return false;

            if (raw.StartsWith("63", StringComparison.Ordinal) && raw.Length == 12)
                raw = "0" + raw[2..];
            else if (!raw.StartsWith('0') && raw.Length == 10)
                raw = "0" + raw;

            if (raw.Length != 11 || !raw.StartsWith("09", StringComparison.Ordinal))
                return false;

            digits = raw;
            return true;
        }

        // "(IF MOBILE LIKE GCASH IT SHOULD BE) '0000009766140473 = 16 DIGITS"
        // — an apostrophe, five zeros, then the 11-digit number.
        //
        // The apostrophe is written as a real character, per the requested
        // format. Note the template's own ' is normally Excel's "treat as text"
        // marker (and this column's remark calls the value 16 digits "without
        // special characters"), so if weAccess rejects an uploaded value of 17
        // characters, removing the leading quote here is the entire fix.
        public static string BuildMobileCreditorAcctNum(string mobile11)
            => "'00000" + mobile11;

        public static string? DigitsOnly(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var raw = new string(value.Where(char.IsDigit).ToArray());
            return raw.Length == 0 ? null : raw;
        }
    }
}
