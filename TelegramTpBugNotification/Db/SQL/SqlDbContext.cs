using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using TelegramTpBugNotification.Db.Models;

namespace TelegramTpBugNotification.Db.SQL
{
    public class SqlDbContext
    {
        private const string SqlConnectingStringConfigKey = "TelegramBotSqlConnectionString";
        private const string TargetProcessBaseUrlConfigKey = "AppSettings:TargetProcessBaseUrl";

        private const string GetTpUserSqlText = @"
SELECT * FROM TpUser u
WHERE u.Login = @loginParam";

        private const string GetMyBugsSqlText = @"
SELECT 
	b.BugID as Id,
	bh.Name, 
	(CASE WHEN e.Name = 'New' THEN 0 
		  WHEN e.Name = 'In Progress' THEN 1
		  WHEN e.Name = 'Implemented' THEN 2
		  WHEN e.Name = 'Revieved' THEN 3
		  WHEN e.Name = 'Verifying' THEN 4
	 END) as State,
	'{0}restui/board.aspx#page=bug/' + CAST(b.BugID AS NVARCHAR(MAX)) as Url
FROM Bug b
	JOIN BugHistory bh ON bh.BugID = b.BugID AND 
		bh.ID = (SELECT MAX(ID) FROM BugHistory bh2 WHERE bh2.BugID = b.BugID)
	JOIN Assignable a ON a.AssignableID = b.BugID
	JOIN Team t ON t.AssignableID = a.AssignableID
	JOIN Role r ON r.RoleID = t.RoleID
	JOIN EntityState e ON e.EntityStateID = a.EntityStateID
	JOIN TpUser u ON u.UserID = t.UserID
WHERE r.Name = 'Developer' AND u.Login = @loginParam AND e.Name IN ('New', 'In Progress', 'Implemented', 'Revieved', 'Verifying') AND
	EXISTS(SELECT * FROM Feature f
		   JOIN Assignable a2 ON a2.AssignableID = f.FeatureID
		   JOIN EntityState e2 ON e2.EntityStateID = a2.EntityStateID
		   WHERE f.FeatureID = b.FeatureID AND e2.Name != 'Done') AND
	DATEDIFF(MONTH, a.LastStateChangeDate, GETDATE()) <= 3";

        private readonly string _connectionString;
        private readonly string _targetProcessBaseUrl;

        public SqlDbContext(IConfigurationRoot configuration)
        {
            _connectionString = configuration.GetConnectionString(SqlConnectingStringConfigKey);
            _targetProcessBaseUrl = configuration[TargetProcessBaseUrlConfigKey];
        }

        private void AddUserLoginParam(SqlCommand sqlCommand, string userLogin)
        {
            var loginParameter = new SqlParameter("@loginParam", SqlDbType.NVarChar)
            {
                Value = userLogin
            };

            sqlCommand.Parameters.Add(loginParameter);
        }

        public bool CheckTpUserExists(string userLogin)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                using (var sqlCommand = new SqlCommand(GetTpUserSqlText, connection))
                {
                    AddUserLoginParam(sqlCommand, userLogin);

                    var result = sqlCommand.ExecuteScalar();
                    return result != null && (int) result > 0;
                }
            }
        }

        public IList<Bug> GetTpUserOpenBugs(User user)
        {
            var userBugs = new List<Bug>();
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                using (var sqlCommand = new SqlCommand(
                    string.Format(GetMyBugsSqlText, _targetProcessBaseUrl), connection))
                {
                    AddUserLoginParam(sqlCommand, user.TpUserLogin);

                    using (var reader = sqlCommand.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            userBugs.Add(
                                new Bug
                                {
                                    Id = (int) reader[nameof(Bug.Id)],
                                    TelegramUserId = user.TelegramUserId,
                                    Name = (string) reader[nameof(Bug.Name)],
                                    State = (Bug.BugState) reader[nameof(Bug.State)],
                                    Url = (string) reader[nameof(Bug.Url)]
                                });
                        }
                    }
                }
            }

            return userBugs;
        }
    }
}
