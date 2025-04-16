using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace DATS.Web.Models;

public enum TicketStatus
{
    Open,
    InProgress,
    Resolved,
    Closed
}

public class Ticket
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public TicketStatus Status { get; set; } = TicketStatus.Open;

    [Required]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    [Required]
    public Guid ReporterUserId { get; set; }
    public virtual User? ReporterUser { get; set; }

    public Guid? AssigneeUserId { get; set; }
    public virtual User? AssigneeUser { get; set; }


    [Column(TypeName = "jsonb")]
    public string? CustomFields { get; set; }

    public virtual ICollection<ImageAttachment> ImageAttachments { get; set; } = new List<ImageAttachment>();
    

    public virtual ICollection<TicketReply> Replies { get; set; } = new List<TicketReply>();


    public Dictionary<string, object?> GetCustomFields()
    {
        if (string.IsNullOrEmpty(CustomFields))
        {
            return new Dictionary<string, object?>();
        }
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(CustomFields) ?? new Dictionary<string, object?>();
    }

    public void SetCustomFields(Dictionary<string, object?> fields)
    {
        CustomFields = JsonSerializer.Serialize(fields);
    }
}