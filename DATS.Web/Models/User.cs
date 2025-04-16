using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DATS.Web.Models;

public enum UserRole
{
    User,
    Agent,
    Admin
}

public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(256)]
    public string OidcSubjectId { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string OidcIssuer { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Name { get; set; }

    [Required]
    public UserRole Role { get; set; } = UserRole.User;

    [Required]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    

    public bool ForceLogout { get; set; } = false;

    public bool IsActive { get; set; } = true;


    public virtual ICollection<Ticket> ReportedTickets { get; set; } = new List<Ticket>();
    public virtual ICollection<Ticket> AssignedTickets { get; set; } = new List<Ticket>();
}