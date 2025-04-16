using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DATS.Web.Models;

public class TicketReply
{
    [Key]
    public Guid Id { get; set; }
    
    [Required]
    public string Content { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; }
    

    public Guid TicketId { get; set; }
    
    [ForeignKey("TicketId")]
    public Ticket Ticket { get; set; }
    

    public Guid AuthorUserId { get; set; }
    
    [ForeignKey("AuthorUserId")]
    public User AuthorUser { get; set; }
    

    public bool IsInternal { get; set; } = false;
}


public class TicketReplyCreateDto
{
    [Required]
    public string Content { get; set; }
    
    public bool IsInternal { get; set; } = false;
}


public class TicketReplyViewDto
{
    public Guid Id { get; set; }
    public string Content { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid AuthorUserId { get; set; }
    public string AuthorEmail { get; set; }
    public string AuthorName { get; set; }
    public bool IsInternal { get; set; }
}