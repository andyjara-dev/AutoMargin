using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Remates.Infrastructure.Entities;

namespace Remates.Infrastructure.Persistence.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> b)
    {
        b.ToTable("app_user");
        b.Property(x => x.FullName).HasMaxLength(160);
    }
}

public class AppRoleConfiguration : IEntityTypeConfiguration<AppRole>
{
    public void Configure(EntityTypeBuilder<AppRole> b)
    {
        b.ToTable("app_role");
        b.Property(x => x.Description).HasMaxLength(240);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_token");

        // Se guarda el hash, nunca el token en claro: si alguien lee la tabla, no puede suplantar a nadie.
        b.Property(x => x.TokenHash).HasMaxLength(120).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();

        b.HasOne(x => x.User).WithMany(u => u.RefreshTokens)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        b.Ignore(x => x.IsActive);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_log");

        b.Property(x => x.EntityName).HasMaxLength(120).IsRequired();
        b.Property(x => x.EntityId).HasMaxLength(60).IsRequired();
        b.Property(x => x.UserName).HasMaxLength(160);
        b.Property(x => x.ChangesJson).HasColumnType("jsonb");

        b.HasIndex(x => new { x.EntityName, x.EntityId });
        b.HasIndex(x => x.OccurredAt);
    }
}
