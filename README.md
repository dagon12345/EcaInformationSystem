# ECA Information System

A case-management and field-operations platform for a local government social welfare office,
built around the Philippines' senior citizen pension program (OSCA/NCSC). It replaces
spreadsheet-based tracking with a single system for beneficiary records, field verification,
payroll, and internal team coordination.

**My role**: sole developer — architecture, backend, frontend, deployment, and ongoing maintenance,
end to end.

## What it does

- **Beneficiary lifecycle management** — application intake, batching, transfers between
  jurisdictions, duplicate-record detection, verification checklists, and findings/audit trail
  per beneficiary.
- **Remote liveness verification** — a public, link-based flow (`LivenessCheckPublic`) that lets
  a beneficiary or their focal person complete a liveness check from their own device, with
  real-time status pushed back to office staff over SignalR.
- **Jurisdiction-scoped invites** — `FocalInvite` issues scoped invite links so a "focal person"
  can be linked to specific jurisdictions without giving them broader system access.
- **Payments & reporting** — payroll runs, liquidation records, payment history per beneficiary,
  and generated PDF/Excel/Word reports (QuestPDF, ClosedXML, OpenXML) plus QR-coded documents.
- **Biometric device sync** — `LocalSyncService`, an installable Windows/Linux background
  service, syncs biometric enrollment data from field devices back to the central database.
- **Internal collaboration layer** — team chat, a post feed with leaderboards, sticky notes, and
  system update notices, all delivered live via SignalR hubs.
- **Security** — JWT authentication with TOTP-based two-factor login (Otp.NET + QRCoder),
  password reset flow, and ASP.NET Core Data Protection with a persisted (EF Core) key ring.

## Architecture

Clean Architecture with a strict inward dependency rule — `Domain` has no outward dependencies,
and every other layer points toward it:

```
EcaInformationSystem.Domain          entities, value objects, enums — no dependencies
EcaInformationSystem.Application     use cases (MediatR), DTOs, FluentValidation → Domain, Shared
EcaInformationSystem.Infrastructure  EF Core, SQL Server, repositories        → Application, Domain
EcaInformationSystem.Api             ASP.NET Core Web API, SignalR hubs,
                                      hosts the compiled Blazor client        → Application, Infrastructure, Client
EcaInformationSystem.Client          Blazor WebAssembly UI                   → Shared
EcaInformationSystem.Shared          DTOs/contracts shared by Api and Client
EcaInformationSystem.Common          cross-cutting constants/utilities
```

`EcaInformationSystem.Web` is a separate, lighter ASP.NET Core host used as an alternate entry
point during development.

## Tech stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core (.NET 10), EF Core + SQL Server, MediatR, FluentValidation |
| Real-time | SignalR (chat, liveness notifications, document/application tracking, live feed) |
| Auth | JWT bearer tokens, TOTP 2FA (Otp.NET), ASP.NET Core Data Protection |
| Frontend | Blazor WebAssembly, Havit Blazor (Bootstrap) component library |
| Documents | QuestPDF, itext7, PDFsharp, ClosedXML, DocumentFormat.OpenXml, QRCoder, ImageSharp |
| Ops | PowerShell deploy scripts (build + SFTP publish), Docker Compose for local SQL Server |

## Code highlights

**TOTP two-factor authentication** (`EcaInformationSystem.Application/Services/AuthService.cs`) —
secret generation, QR enrollment, and a constrained verification window on login:

```csharp
public async Task<MfaSetupResult> GenerateMfaSecretAsync(Guid userId)
{
    var user = await _pendingUserRegistrationRepository.GetByIdAsync(userId)
        ?? throw new KeyNotFoundException("User not found.");

    var secretKey = KeyGeneration.GenerateRandomKey(20); // 160-bit, standard for TOTP
    var base32Secret = Base32Encoding.ToString(secretKey);

    // Encrypt before persisting — plain secret only ever leaves this method in the response
    user.MfaSecret = _mfaProtector.Protect(base32Secret);
    user.MfaSetupComplete = false;
    await _pendingUserRegistrationRepository.SaveChangesAsync();

    var otpauthUri = $"otpauth://totp/ECA-InFORMS:{user.UserName}?secret={base32Secret}&issuer=ECA-InFORMS&digits=6&period=30";
    using var qrGenerator = new QRCodeGenerator();
    var qrCode = new PngByteQRCode(qrGenerator.CreateQrCode(otpauthUri, QRCodeGenerator.ECCLevel.Q));

    return new MfaSetupResult
    {
        Secret = base32Secret, // shown once, for manual entry / QR fallback
        QrCodeImageBase64 = Convert.ToBase64String(qrCode.GetGraphic(10))
    };
}

public async Task<AuthResult> VerifyMfaAndIssueTokenAsync(string userId, string code, string ipAddress, string userAgent)
{
    var user = await _pendingUserRegistrationRepository.GetByIdAsync(Guid.Parse(userId));
    var plainSecret = _mfaProtector.Unprotect(user.MfaSecret);
    var totp = new Totp(Base32Encoding.ToBytes(plainSecret));
    var isValid = totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));

    if (!isValid) return AuthResult.Failed("Invalid or expired code.");
    // ...issue JWT
}
```

The MFA secret is never stored in plaintext (`IDataProtector`-encrypted at rest), and the plain
value is only ever returned once, at enrollment, for the QR/manual-entry step.

## Running locally

```
dotnet restore EcaInformationSystem.slnx
dotnet run --project EcaInformationSystem.Api
```

The Api project hosts the compiled Blazor Client as static assets, so running it serves the full
app. Local SQL Server can be started with `docker-compose up`; connection settings live in each
project's `appsettings.Development.json` (gitignored — not included in this repo).

## Status

Actively developed — recent work includes the liveness-check flow, focal-person invite links,
and dashboard UI passes (see commit history).
