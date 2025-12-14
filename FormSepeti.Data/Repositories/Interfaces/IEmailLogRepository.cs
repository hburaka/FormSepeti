using System.Threading.Tasks;
using FormSepeti.Data.Entities;

namespace FormSepeti.Data.Repositories.Interfaces
{
    public interface IEmailLogRepository
    {
        Task<EmailLog> CreateAsync(EmailLog log);
    }
}