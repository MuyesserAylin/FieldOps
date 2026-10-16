using System;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {

    }
}
