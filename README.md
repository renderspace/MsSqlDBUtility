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
All methods have similar signature, just use the one that matches your required output.
```cs
public static int ExecuteNonQuery(string connString, CommandType cmdType, string cmdText, params SqlParameter[] cmdParms)
public static IDataReader ExecuteReader(string connString, CommandType cmdType, string cmdText, params SqlParameter[] cmdParms)
public static object ExecuteScalar(string connString, CommandType cmdType, string cmdText, params SqlParameter[] cmdParms)
public static DataSet ExecuteDataset(string connString, CommandType commandType, string commandText, params SqlParameter[] commandParameters)
```


