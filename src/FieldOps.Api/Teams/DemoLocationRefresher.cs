using FieldOps.Api.Data;
using FieldOps.Api.Time;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Api.Teams;

// SADECE DEMO İÇİN: demo ekiplerin konumu hiç güncellenmediği için eleme kuralı 4 ("konum 60 sn'den eskiyse aday olamaz")
// bir süre sonra hepsini eler. Bu servis tüm ekiplerin konum zamanını "şimdi" yapar; konumun kendisi değişmez (ekipler yerinde durur).
// Ay 3'te gerçek konum ucu (POST /api/ekip/konum) aynı Team.UpdateLocation metodunu kullanacak.
// Yalnızca demo modunda (Seed:DemoTeams açık) çalışır; üretimde açılamaz.
public sealed class DemoLocationRefresher
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly IConfiguration _configuration;

    public DemoLocationRefresher(AppDbContext db, IClock clock, IConfiguration configuration)
    {
        _db = db;
        _clock = clock;
        _configuration = configuration;
    }

    public bool IsEnabled =>
        bool.TryParse(_configuration["Seed:DemoTeams"], out var enabled) && enabled;

    public async Task<int> RefreshAllAsync(CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException(
                "Konum yenileme yalnızca demo modunda kullanılabilir (Seed:DemoTeams açık olmalı).");
        }

        var now = _clock.UtcNow;
        var teams = await _db.Teams.ToListAsync(cancellationToken);

        var refreshed = 0;
        foreach (var team in teams)
        {
            if (team.LocationUpdatedAt > now)
            {
                continue;
            }

            team.UpdateLocation(team.Location, now);
            refreshed++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return refreshed;
    }
}