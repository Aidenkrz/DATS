using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DATS.Web.Models;

public class ImageAttachment
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid TicketId { get; set; }
    [ForeignKey("TicketId")]
    public virtual Ticket? Ticket { get; set; }

    [Required]
    [MaxLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string ContentType { get; set; } = string.Empty;

    [Required]
    public long FileSize { get; set; }

    [Required]
    public string StoredFilePath { get; set; } = string.Empty;

    [Required]
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;


    public bool MarkedForDeletion { get; set; } = false;
    public DateTimeOffset? DeletionScheduledAt { get; set; }
}