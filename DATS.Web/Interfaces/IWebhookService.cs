using DATS.Web.Models;
using System.Threading.Tasks;

namespace DATS.Web.Interfaces
{
    public interface IWebhookService
    {





        Task SendTicketCreatedWebhookAsync(Ticket ticket);







        Task SendTicketStatusChangedWebhookAsync(Ticket ticket, TicketStatus oldStatus);
    }
}