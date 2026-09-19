using Microsoft.EntityFrameworkCore;

namespace Keylet.Data;

internal sealed class KeyletDbContext(DbContextOptions<KeyletDbContext> options) : DbContext(options);

