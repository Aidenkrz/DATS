using DATS.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace DATS.Web.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Ticket> Tickets { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<ImageAttachment> ImageAttachments { get; set; }
    public DbSet<TicketReply> TicketReplies { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);




        modelBuilder.Entity<Ticket>()
            .HasOne(t => t.ReporterUser)
            .WithMany(u => u.ReportedTickets)
            .HasForeignKey(t => t.ReporterUserId)
            .OnDelete(DeleteBehavior.Restrict);


        modelBuilder.Entity<Ticket>()
            .HasOne(t => t.AssigneeUser)
            .WithMany(u => u.AssignedTickets)
            .HasForeignKey(t => t.AssigneeUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);


        modelBuilder.Entity<ImageAttachment>()
            .HasOne(ia => ia.Ticket)
            .WithMany(t => t.ImageAttachments)
            .HasForeignKey(ia => ia.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
            

        modelBuilder.Entity<TicketReply>()
            .HasOne(tr => tr.Ticket)
            .WithMany(t => t.Replies)
            .HasForeignKey(tr => tr.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
            

        modelBuilder.Entity<TicketReply>()
            .HasOne(tr => tr.AuthorUser)
            .WithMany()
            .HasForeignKey(tr => tr.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);


        modelBuilder.Entity<User>()
            .HasIndex(u => new { u.OidcSubjectId, u.OidcIssuer })
            .IsUnique();






    }
}