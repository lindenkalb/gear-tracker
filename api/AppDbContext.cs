using Microsoft.EntityFrameworkCore;

namespace GearTracker.Api;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{

}
