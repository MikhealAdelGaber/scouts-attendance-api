using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Application.DTOs.Transfers;
using ScoutsAttendance.Application.Interfaces;

namespace ScoutsAttendance.Application.Services;

public interface ITransferService
{
    Task<IEnumerable<TransferDto>> GetMemberTransfersAsync(Guid memberId);
    Task<TransferDto> CreateTransferAsync(CreateTransferDto dto);
}

public class TransferService : ITransferService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public TransferService(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<TransferDto>> GetMemberTransfersAsync(Guid memberId)
    {
        var transfers = await _uow.Transfers.Query()
            .Include(t => t.Member)
            .Include(t => t.FromTroop)
            .Include(t => t.ToTroop)
            .Where(t => t.MemberId == memberId && !t.IsDeleted)
            .OrderByDescending(t => t.TransferDate)
            .ToListAsync();

        return transfers.Select(t => new TransferDto
        {
            Id = t.Id,
            MemberId = t.MemberId,
            MemberName = t.Member?.FullName ?? string.Empty,
            FromTroopId = t.FromTroopId,
            FromTroopName = t.FromTroop?.Name ?? string.Empty,
            ToTroopId = t.ToTroopId,
            ToTroopName = t.ToTroop?.Name ?? string.Empty,
            TransferDate = t.TransferDate,
            Reason = t.Reason,
            CreatedAt = t.CreatedAt
        });
    }

    public async Task<TransferDto> CreateTransferAsync(CreateTransferDto dto)
    {
        var member = await _uow.Members.GetByIdAsync(dto.MemberId)
            ?? throw new KeyNotFoundException("Member not found");

        if (member.TroopId == null)
            throw new InvalidOperationException(
                "Cannot transfer an unassigned member. Assign them to a troop first.");

        var toTroop = await _uow.Troops.GetByIdAsync(dto.ToTroopId)
            ?? throw new KeyNotFoundException("Target troop not found");

        var transfer = new Domain.Entities.Transfer
        {
            MemberId = dto.MemberId,
            FromTroopId = member.TroopId.Value,
            ToTroopId = dto.ToTroopId,
            TransferDate = dto.TransferDate,
            Reason = dto.Reason,
            ApprovedBy = _currentUser.UserId
        };

        member.TroopId = dto.ToTroopId;
        member.GroupId = toTroop.GroupId;
        member.UpdatedAt = DateTime.UtcNow;

        await _uow.Transfers.AddAsync(transfer);
        _uow.Members.Update(member);
        await _uow.SaveChangesAsync();

        return new TransferDto
        {
            Id = transfer.Id,
            MemberId = member.Id,
            MemberName = member.FullName,
            FromTroopId = transfer.FromTroopId,
            ToTroopId = transfer.ToTroopId,
            ToTroopName = toTroop.Name,
            TransferDate = transfer.TransferDate,
            Reason = transfer.Reason,
            CreatedAt = transfer.CreatedAt
        };
    }
}
