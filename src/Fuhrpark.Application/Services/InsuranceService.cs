using Fuhrpark.Application.Abstractions;
using Fuhrpark.Application.Common;
using Fuhrpark.Domain.Entities;
using Fuhrpark.Domain.Exceptions;
using Fuhrpark.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace Fuhrpark.Application.Services;

/// <inheritdoc />
public sealed class InsuranceService : IInsuranceService
{
    private readonly IFuhrparkDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public InsuranceService(IFuhrparkDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<VehicleInsurance>> GetForVehicleAsync(
        int vehicleId,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.InsuranceView);

        return await _db.VehicleInsurances
            .AsNoTracking()
            .Where(i => i.VehicleId == vehicleId)
            .OrderByDescending(i => i.IsActive)
            .ThenByDescending(i => i.ValidFrom)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<VehicleInsurance?> GetActiveAsync(int vehicleId, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.InsuranceView);

        return await _db.VehicleInsurances
            .AsNoTracking()
            .Where(i => i.VehicleId == vehicleId && i.IsActive)
            .OrderByDescending(i => i.ValidFrom)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> CreateAsync(VehicleInsurance insurance, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.InsuranceManage);

        insurance.Company = Guard.NotEmpty(insurance.Company, "Versicherung");

        Guard.That(insurance.ValidTo is null || insurance.ValidFrom is null || insurance.ValidTo >= insurance.ValidFrom,
            "Das Vertragsende darf nicht vor dem Vertragsbeginn liegen.");

        _db.VehicleInsurances.Add(insurance);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return insurance.Id;
    }

    public async Task UpdateAsync(VehicleInsurance insurance, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.InsuranceManage);

        var existing = await _db.VehicleInsurances
            .FirstOrDefaultAsync(i => i.Id == insurance.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(VehicleInsurance), insurance.Id);

        existing.Company = Guard.NotEmpty(insurance.Company, "Versicherung");
        existing.PolicyNumber = insurance.PolicyNumber;
        existing.ContractNumber = insurance.ContractNumber;
        existing.Kind = insurance.Kind;
        existing.ValidFrom = insurance.ValidFrom;
        existing.ValidTo = insurance.ValidTo;
        existing.ContactPerson = insurance.ContactPerson;
        existing.Phone = insurance.Phone;
        existing.Email = insurance.Email;
        existing.AnnualPremium = insurance.AnnualPremium;
        existing.DeductibleComprehensive = insurance.DeductibleComprehensive;
        existing.DeductiblePartial = insurance.DeductiblePartial;
        existing.IsActive = insurance.IsActive;
        existing.Comment = insurance.Comment;
        existing.RowVersion = insurance.RowVersion;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeactivateAsync(int insuranceId, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.InsuranceManage);

        var insurance = await _db.VehicleInsurances
            .FirstOrDefaultAsync(i => i.Id == insuranceId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(VehicleInsurance), insuranceId);

        insurance.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <inheritdoc />
public sealed class VehicleKeyService : IVehicleKeyService
{
    private readonly IFuhrparkDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public VehicleKeyService(IFuhrparkDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<VehicleKey>> GetForVehicleAsync(
        int vehicleId,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.KeyView);

        return await _db.VehicleKeys
            .AsNoTracking()
            .Include(k => k.IssuedToDriver)
            .Where(k => k.VehicleId == vehicleId)
            .OrderBy(k => k.KeyNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> CreateAsync(VehicleKey key, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.KeyManage);

        key.KeyNumber = Guard.NotEmpty(key.KeyNumber, "Schluesselnummer");
        Guard.That(key.Count > 0, "Die Anzahl muss mindestens 1 betragen.");

        var duplicate = await _db.VehicleKeys
            .AnyAsync(k => k.VehicleId == key.VehicleId && k.KeyNumber == key.KeyNumber, cancellationToken)
            .ConfigureAwait(false);
        Guard.That(!duplicate, $"Die Schluesselnummer '{key.KeyNumber}' ist fuer dieses Fahrzeug bereits erfasst.");

        _db.VehicleKeys.Add(key);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return key.Id;
    }

    public async Task UpdateAsync(VehicleKey key, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.KeyManage);

        var existing = await _db.VehicleKeys
            .FirstOrDefaultAsync(k => k.Id == key.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(VehicleKey), key.Id);

        existing.KeyNumber = Guard.NotEmpty(key.KeyNumber, "Schluesselnummer");
        existing.Count = key.Count;
        existing.StorageLocation = key.StorageLocation;
        existing.Comment = key.Comment;
        existing.RowVersion = key.RowVersion;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task IssueAsync(
        int keyId,
        int? driverId,
        string? issuedToName,
        DateTime issuedAt,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.KeyManage);

        var key = await _db.VehicleKeys
            .FirstOrDefaultAsync(k => k.Id == keyId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(VehicleKey), keyId);

        Guard.That(!key.IsIssued, "Dieser Schluessel ist bereits ausgegeben.");
        Guard.That(driverId is not null || !string.IsNullOrWhiteSpace(issuedToName),
            "Bitte einen Fahrer auswaehlen oder einen Namen angeben.");

        key.IssuedToDriverId = driverId;
        key.IssuedToName = issuedToName;
        key.IssuedAt = issuedAt;
        key.ReturnedAt = null;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ReturnAsync(int keyId, DateTime returnedAt, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.KeyManage);

        var key = await _db.VehicleKeys
            .FirstOrDefaultAsync(k => k.Id == keyId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(VehicleKey), keyId);

        Guard.That(key.IsIssued, "Dieser Schluessel ist derzeit nicht ausgegeben.");

        key.ReturnedAt = returnedAt;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(int keyId, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.KeyManage);

        var key = await _db.VehicleKeys
            .FirstOrDefaultAsync(k => k.Id == keyId, cancellationToken)
            .ConfigureAwait(false);

        if (key is null)
        {
            return;
        }

        Guard.That(!key.IsIssued, "Ein ausgegebener Schluessel kann nicht geloescht werden.");

        _db.VehicleKeys.Remove(key);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
