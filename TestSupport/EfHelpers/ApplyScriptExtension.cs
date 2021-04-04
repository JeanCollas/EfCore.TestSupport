﻿// Copyright (c) 2017 Jon P Smith, GitHub: JonPSmith, web: http://www.thereformedprogrammer.net/
// Licensed under MIT licence. See License.txt in the project root for license information.

using System.Data;
#if NETSTANDARD2_0
using System.Data.SqlClient;
#elif NETSTANDARD2_1
using Microsoft.Data.SqlClient;
#endif
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace TestSupport.EfHelpers
{
    /// <summary>
    /// Static class holding extension methods for applying SQL scripts to a database
    /// </summary>
    public static class ApplyScriptExtension
    {

        /// <summary>
        /// This reads in a SQL script file and executes each command to the database pointed at by the DbContext
        /// Each command should have an GO at the start of the line after the command.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="filePath"></param>
        public static void ExecuteScriptFileInTransaction(this DbContext context, string filePath)
        {
            var scriptContent = File.ReadAllText(filePath);
            var regex = new Regex("^GO", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            var commands = regex.Split(scriptContent).Select(x => x.Trim());

            using (var transaction = context.Database.BeginTransaction(IsolationLevel.ReadUncommitted))
            {
                foreach (var command in commands)
                {
                    if (command.Length > 0)
                    {
                        try
                        {
#if NETSTANDARD2_0
                            context.Database.ExecuteSqlCommand(command);
#elif NETSTANDARD2_1
                            context.Database.ExecuteSqlRaw(command);
#endif
                        }
                        catch (SqlException)
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
                transaction.Commit();
            }
        }
    }
}