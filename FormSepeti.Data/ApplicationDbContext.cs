using Microsoft.EntityFrameworkCore;
using FormSepeti.Data.Entities;

namespace FormSepeti.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSets
        public DbSet<User> Users { get; set; }
        public DbSet<FormGroup> FormGroups { get; set; }
        public DbSet<Package> Packages { get; set; }
        public DbSet<Form> Forms { get; set; }
        public DbSet<FormGroupMapping> FormGroupMappings { get; set; }
        public DbSet<UserPackage> UserPackages { get; set; }
        public DbSet<UserGoogleSheet> UserGoogleSheets { get; set; }
        public DbSet<FormSubmission> FormSubmissions { get; set; }
        public DbSet<EmailLog> EmailLogs { get; set; }
        public DbSet<SmsLog> SmsLogs { get; set; }
        public DbSet<AdminUser> AdminUsers { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.UserId);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.ActivationToken);
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
                entity.Property(e => e.PasswordHash).IsRequired();
            });

            // FormGroup
            modelBuilder.Entity<FormGroup>(entity =>
            {
                entity.HasKey(e => e.GroupId);
                entity.Property(e => e.GroupName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            });

            // Package
            modelBuilder.Entity<Package>(entity =>
            {
                entity.HasKey(e => e.PackageId);
                entity.HasOne(e => e.FormGroup)
                    .WithMany()
                    .HasForeignKey(e => e.GroupId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.Property(e => e.PackageName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            });

            // Form
            modelBuilder.Entity<Form>(entity =>
            {
                entity.HasKey(e => e.FormId);
                entity.HasIndex(e => e.JotFormId);
                entity.Property(e => e.FormName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.JotFormId).IsRequired().HasMaxLength(50);
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            });

            // FormGroupMapping
            modelBuilder.Entity<FormGroupMapping>(entity =>
            {
                entity.HasKey(e => e.MappingId);
                entity.HasIndex(e => new { e.FormId, e.GroupId }).IsUnique();
                entity.HasOne(e => e.Form)
                    .WithMany()
                    .HasForeignKey(e => e.FormId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.FormGroup)
                    .WithMany()
                    .HasForeignKey(e => e.GroupId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // UserPackage
            modelBuilder.Entity<UserPackage>(entity =>
            {
                entity.HasKey(e => e.UserPackageId);
                entity.HasIndex(e => new { e.UserId, e.IsActive });
                entity.HasIndex(e => e.PaymentTransactionId);
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Package)
                    .WithMany()
                    .HasForeignKey(e => e.PackageId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.FormGroup)
                    .WithMany()
                    .HasForeignKey(e => e.GroupId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.Property(e => e.PurchaseDate).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.PaymentAmount).HasColumnType("decimal(18,2)");
            });

            // UserGoogleSheet
            modelBuilder.Entity<UserGoogleSheet>(entity =>
            {
                entity.HasKey(e => e.SheetId);
                entity.HasIndex(e => new { e.UserId, e.GroupId }).IsUnique();
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.FormGroup)
                    .WithMany()
                    .HasForeignKey(e => e.GroupId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.Property(e => e.SpreadsheetId).IsRequired().HasMaxLength(100);
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            });

            // FormSubmission
            modelBuilder.Entity<FormSubmission>(entity =>
            {
                entity.HasKey(e => e.SubmissionId);
                entity.HasIndex(e => new { e.UserId, e.SubmittedDate });
                entity.HasIndex(e => e.JotFormSubmissionId);
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Form)
                    .WithMany()
                    .HasForeignKey(e => e.FormId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.FormGroup)
                    .WithMany()
                    .HasForeignKey(e => e.GroupId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.Property(e => e.SubmittedDate).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.Status).HasMaxLength(50);
            });

            // EmailLog
            modelBuilder.Entity<EmailLog>(entity =>
            {
                entity.HasKey(e => e.LogId);
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.Property(e => e.EmailTo).IsRequired().HasMaxLength(255);
                entity.Property(e => e.SentDate).HasDefaultValueSql("GETUTCDATE()");
            });

            // SmsLog
            modelBuilder.Entity<SmsLog>(entity =>
            {
                entity.HasKey(e => e.LogId);
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.Property(e => e.PhoneNumber).IsRequired().HasMaxLength(20);
                entity.Property(e => e.SentDate).HasDefaultValueSql("GETUTCDATE()");
            });

            // AdminUser
            modelBuilder.Entity<AdminUser>(entity =>
            {
                entity.HasKey(e => e.AdminId);
                entity.HasIndex(e => e.Username).IsUnique();
                entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
                entity.Property(e => e.PasswordHash).IsRequired();
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            });

            // AuditLog
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasKey(e => e.LogId);
                entity.HasIndex(e => e.CreatedDate);
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(e => e.AdminUser)
                    .WithMany()
                    .HasForeignKey(e => e.AdminId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.Property(e => e.Action).IsRequired().HasMaxLength(200);
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            });
        }
    }
}