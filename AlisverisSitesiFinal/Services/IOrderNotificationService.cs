using System.Threading.Tasks;

namespace AlisverisSitesiFinal.Services
{
    public interface IOrderNotificationService
    {
        Task NotifySellersAsync(int siparisId);
    }
}
