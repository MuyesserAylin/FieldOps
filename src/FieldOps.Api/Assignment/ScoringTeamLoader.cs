using FieldOps.Api.Data;
using FieldOps.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Api.Assignment;

// Elemeden geçen adayların puanlama "karnesini" (ScoringTeam) hazırlar.
// SADECE OKUR: veritabanında hiçbir şeyi değiştirmez, puanlamaz, karar vermez.
// Sadece elemeden geçmiş ekipleri alır: bu ekiplerin işin istediği becerisi ve şu an kapsayan vardiyası vardır.
// Böyle bir ekip için beceri ya da vardiya bulunamazsa hata verir (eleme çalışmadan gelinmiş demektir).
// Vardiya kuralı eleme ile aynıdır: başlangıç dahil, bitiş hariç.
public sealed class ScoringTeamLoader
{
    private readonly AppDbContext _db;

    public ScoringTeamLoader(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ScoringTeam>> LoadAsync(
        JobRequest job,
        IReadOnlyCollection<TeamSnapshot> candidates,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(candidates);

        if (candidates.Count == 0)
        {
            return Array.Empty<ScoringTeam>();
        }

        var shiftByTeam = new Dictionary<int, ShiftWindow>();
        foreach (var candidate in candidates)
        {
            var shift = candidate.Shifts
                .Where(s => s.StartsAt <= now && now < s.EndsAt)
                .OrderByDescending(s => s.StartsAt)
                .FirstOrDefault();

            if (shift is null)
            {
                throw new InvalidOperationException(
                    $"Ekip {candidate.TeamId} aday ama şu an vardiyasında değil; eleme bunu çıkarmış olmalıydı.");
            }

            if (!shiftByTeam.TryAdd(candidate.TeamId, shift))
            {
                throw new ArgumentException($"Ekip {candidate.TeamId} adaylar arasında birden fazla kez var.", nameof(candidates));
            }
        }

        var teamIds = shiftByTeam.Keys.ToList();

        var levels = await _db.Set<TeamSkill>()
            .AsNoTracking()
            .Where(ts => ts.SkillId == job.RequiredSkillId && teamIds.Contains(ts.TeamId))
            .ToDictionaryAsync(ts => ts.TeamId, ts => ts.Level, cancellationToken);

        var windowStart = shiftByTeam.Values.Min(s => s.StartsAt);
        var windowEnd = shiftByTeam.Values.Max(s => s.EndsAt);

        var assignedJobs = await _db.Jobs
            .AsNoTracking()
            .Where(j => j.AssignedTeamId != null
                        && teamIds.Contains(j.AssignedTeamId.Value)
                        && j.AssignedAt >= windowStart
                        && j.AssignedAt < windowEnd
                        && j.Status != JobStatus.Cancelled)
            .Select(j => new
            {
                TeamId = j.AssignedTeamId!.Value,
                AssignedAt = j.AssignedAt!.Value,
                Minutes = j.FaultType.EstimatedDurationMinutes
            })
            .ToListAsync(cancellationToken);

        var result = new List<ScoringTeam>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var shift = shiftByTeam[candidate.TeamId];

            if (!levels.TryGetValue(candidate.TeamId, out var level))
            {
                throw new InvalidOperationException(
                    $"Ekip {candidate.TeamId} aday ama işin istediği beceriye ({job.RequiredSkillId}) sahip değil; eleme bunu çıkarmış olmalıydı.");
            }

            var workedMinutes = assignedJobs
                .Where(a => a.TeamId == candidate.TeamId && a.AssignedAt >= shift.StartsAt && a.AssignedAt < shift.EndsAt)
                .Sum(a => (double)a.Minutes);

            var shiftMinutes = (shift.EndsAt - shift.StartsAt).TotalMinutes;

            // OffersConsidered / OffersAccepted: teklif tablosu Ay 3'te gelince doldurulacak. Şimdilik 0 / 0 (GeçmişCevap notu 100).
            result.Add(new ScoringTeam(
                candidate.TeamId,
                candidate.DistanceKm,
                workedMinutes,
                shiftMinutes,
                level,
                OffersConsidered: 0,
                OffersAccepted: 0));
        }

        return result;
    }
}