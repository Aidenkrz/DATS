
using System;
using DATS.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DATS.Web.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20250411232918_AddForceLogoutFlag")]
    partial class AddForceLogoutFlag
    {

        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.4")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("DATS.Web.Models.ImageAttachment", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("ContentType")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<DateTimeOffset?>("DeletionScheduledAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<long>("FileSize")
                        .HasColumnType("bigint");

                    b.Property<bool>("MarkedForDeletion")
                        .HasColumnType("boolean");

                    b.Property<string>("OriginalFileName")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("character varying(255)");

                    b.Property<string>("StoredFilePath")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<Guid>("TicketId")
                        .HasColumnType("uuid");

                    b.Property<DateTimeOffset>("UploadedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("TicketId");

                    b.ToTable("ImageAttachments");
                });

            modelBuilder.Entity("DATS.Web.Models.Ticket", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid?>("AssigneeUserId")
                        .HasColumnType("uuid");

                    b.Property<DateTimeOffset?>("ClosedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("CustomFields")
                        .HasColumnType("jsonb");

                    b.Property<string>("Description")
                        .HasColumnType("text");

                    b.Property<Guid>("ReporterUserId")
                        .HasColumnType("uuid");

                    b.Property<int>("Status")
                        .HasColumnType("integer");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("AssigneeUserId");

                    b.HasIndex("ReporterUserId");

                    b.ToTable("Tickets");
                });

            modelBuilder.Entity("DATS.Web.Models.TicketReply", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("AuthorUserId")
                        .HasColumnType("uuid");

                    b.Property<string>("Content")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<bool>("IsInternal")
                        .HasColumnType("boolean");

                    b.Property<Guid>("TicketId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("AuthorUserId");

                    b.HasIndex("TicketId");

                    b.ToTable("TicketReplies");
                });

            modelBuilder.Entity("DATS.Web.Models.User", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasMaxLength(256)
                        .HasColumnType("character varying(256)");

                    b.Property<bool>("ForceLogout")
                        .HasColumnType("boolean");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean");

                    b.Property<string>("Name")
                        .HasMaxLength(256)
                        .HasColumnType("character varying(256)");

                    b.Property<string>("OidcIssuer")
                        .IsRequired()
                        .HasMaxLength(256)
                        .HasColumnType("character varying(256)");

                    b.Property<string>("OidcSubjectId")
                        .IsRequired()
                        .HasMaxLength(256)
                        .HasColumnType("character varying(256)");

                    b.Property<int>("Role")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("OidcSubjectId", "OidcIssuer")
                        .IsUnique();

                    b.ToTable("Users");
                });

            modelBuilder.Entity("DATS.Web.Models.ImageAttachment", b =>
                {
                    b.HasOne("DATS.Web.Models.Ticket", "Ticket")
                        .WithMany("ImageAttachments")
                        .HasForeignKey("TicketId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Ticket");
                });

            modelBuilder.Entity("DATS.Web.Models.Ticket", b =>
                {
                    b.HasOne("DATS.Web.Models.User", "AssigneeUser")
                        .WithMany("AssignedTickets")
                        .HasForeignKey("AssigneeUserId")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.HasOne("DATS.Web.Models.User", "ReporterUser")
                        .WithMany("ReportedTickets")
                        .HasForeignKey("ReporterUserId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("AssigneeUser");

                    b.Navigation("ReporterUser");
                });

            modelBuilder.Entity("DATS.Web.Models.TicketReply", b =>
                {
                    b.HasOne("DATS.Web.Models.User", "AuthorUser")
                        .WithMany()
                        .HasForeignKey("AuthorUserId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("DATS.Web.Models.Ticket", "Ticket")
                        .WithMany("Replies")
                        .HasForeignKey("TicketId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("AuthorUser");

                    b.Navigation("Ticket");
                });

            modelBuilder.Entity("DATS.Web.Models.Ticket", b =>
                {
                    b.Navigation("ImageAttachments");

                    b.Navigation("Replies");
                });

            modelBuilder.Entity("DATS.Web.Models.User", b =>
                {
                    b.Navigation("AssignedTickets");

                    b.Navigation("ReportedTickets");
                });
#pragma warning restore 612, 618
        }
    }
}
