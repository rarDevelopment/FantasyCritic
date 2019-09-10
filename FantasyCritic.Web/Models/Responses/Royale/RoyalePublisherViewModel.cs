using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FantasyCritic.Lib.Royale;
using NodaTime;

namespace FantasyCritic.Web.Models.Responses.Royale
{
    public class RoyalePublisherViewModel
    {
        public RoyalePublisherViewModel(RoyalePublisher domain, IClock clock)
        {
            PublisherID = domain.PublisherID;
            YearQuarter = new RoyaleYearQuarterViewModel(domain.YearQuarter);
            PlayerName = domain.User.DisplayName;
            PublisherName = domain.PublisherName;
            PublisherGames = domain.PublisherGames.Select(x => new RoyalePublisherGameViewModel(x, clock)).ToList();
            Budget = domain.Budget;
        }

        public Guid PublisherID { get; }
        public RoyaleYearQuarterViewModel YearQuarter { get; }
        public string PlayerName { get; }
        public string PublisherName { get; }
        public IReadOnlyList<RoyalePublisherGameViewModel> PublisherGames { get; }
        public decimal Budget { get; }
    }
}
