# MsSqlDBUtility
A minimal wrapper for accessing MSSQL database. 
Based on [MS Data Access Application Block](http://msdn.microsoft.com/library/en-us/dnbda/html/daab-rm.asp)

## Getting Started

### Prerequisites
Works with .NET framework 4.5. 

### Installing
Using nuget console
```Powershell
Install-Package MsSqlDBUtility 
```
Add to your file
```cs
using MsSqlDBUtility;
using System.Data;
using System.Data.SqlClient;
```
And make your first query (or to be exact "nonquery"):
```cs
var p = new SqlParameter("@id", id);
SqlHelper.ExecuteNonQuery(SqlHelper.ConnStringMain, CommandType.Text, "DELETE FROM table WHERE id=@id ", p);
```
## API
### Methods that run SQL query
All methods have similar signature, just use the one that matches your required output.
```cs
public static int ExecuteNonQuery(string connString, CommandType cmdType, string cmdText, params SqlParameter[] cmdParms)
public static IDataReader ExecuteReader(string connString, CommandType cmdType, string cmdText, params SqlParameter[] cmdParms)
public static object ExecuteScalar(string connString, CommandType cmdType, string cmdText, params SqlParameter[] cmdParms)
public static DataSet ExecuteDataset(string connString, CommandType commandType, string commandText, params SqlParameter[] commandParameters)
```
All of these methods also have overloaded cousins where the first parameter is SqlConnection or SqlTransaction for cases where you really need multiple calls on same connection.
#### Example with old and disliked Dataset
```cs
DataSet dataSet = SqlHelper.ExecuteDataset(SqlHelper.ConnStringMain, CommandType.Text, sql);
```
#### Example with fast IDataReader
```cs
using (var reader = SqlHelper.ExecuteReader(Schema.ConnectionString, CommandType.Text, sql, parameters))
{
    while (reader.Read())
    {
      var x = (int) reader["x"]
      ...
    }
}
``` 
### RunScript
```cs
public static void RunScript(string connString, string sql)
```
For running long scripts where statements are separated by ```GO``` statement. Typical usage would be to generate database schema. 
### BuildConnectionString
```cs
public static string BuildConnectionString(string serverName, string dbName, string dbUsername, string dbPassword)
```
### ListDatabases
```cs
public static List<string> ListDatabases(string connString)
```
Lists user databases (excludes system databases like 'master' and 'tempdb').
## Contributing
Just send a pull request.
## License
Code is licensed under (Microsoft patterns & practices License for Enterprise Library 3.1 - May 2007)[https://msdn.microsoft.com/en-us/library/aa480459.aspx]



