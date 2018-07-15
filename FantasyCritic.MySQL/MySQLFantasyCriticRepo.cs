using CSharpFunctionalExtensions;
using FantasyCritic.Lib.Domain;
using FantasyCritic.Lib.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using FantasyCritic.Lib.Services;
using FantasyCritic.MySQL.Entities;
using MySql.Data.MySqlClient;

namespace FantasyCritic.MySQL
{
    public class MySQLFantasyCriticRepo : IFantasyCriticRepo
    {
        private readonly string _connectionString;
        private readonly IReadOnlyFantasyCriticUserStore _userStore;

        public MySQLFantasyCriticRepo(string connectionString, IReadOnlyFantasyCriticUserStore userStore)
        {
            _connectionString = connectionString;
            _userStore = userStore;
        }

        public async Task<Maybe<FantasyCriticLeague>> GetLeagueByID(Guid id)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                var queryObject = new
                {
                    leagueID = id
                };

                FantasyCriticLeagueEntity leagueEntity = await connection.QuerySingleAsync<FantasyCriticLeagueEntity>(
                    "select * from tblleague where LeagueID = @leagueID", queryObject);

                FantasyCriticUser manager = await _userStore.FindByIdAsync(leagueEntity.LeagueManager.ToString(), CancellationToken.None);

                IEnumerable<LeagueYearEntity> yearEntities = await connection.QueryAsync<LeagueYearEntity>("select * from tblleagueyear where LeagueID = @leagueID", queryObject);
                IEnumerable<int> years = yearEntities.Select(x => x.Year);

                FantasyCriticLeague league = leagueEntity.ToDomain(manager, years);
                return league;
            }
        }

        public Task<IReadOnlyList<Guid>> GetPlayerIDsInLeague(FantasyCriticLeague league)
        {
            throw new NotImplementedException();
        }

        public async Task CreateLeague(FantasyCriticLeague league, int initialYear)
        {
            FantasyCriticLeagueEntity entity = new FantasyCriticLeagueEntity(league);
            LeagueYearEntity leagueYearEntity = new LeagueYearEntity(league, initialYear);

            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.ExecuteAsync(
                    "insert into tblleague(LeagueID,LeagueName,LeagueManager,DraftGames,WaiverGames,AntiPicks,EstimatedGameScore,EligibilitySystem,DraftSystem,WaiverSystem,ScoringSystem) VALUES " +
                    "(@LeagueID,@LeagueName,@LeagueManager,@DraftGames,@WaiverGames,@AntiPicks,@EstimatedGameScore,@EligibilitySystem,@DraftSystem,@WaiverSystem,@ScoringSystem);",
                    entity);

                await connection.ExecuteAsync(
                    "insert into tblleagueyear(LeagueID,Year) VALUES (@LeagueID, @Year);",
                    leagueYearEntity);
            }
        }
    }
}
