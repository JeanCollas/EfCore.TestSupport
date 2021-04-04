﻿// Copyright (c) 2017 Jon P Smith, GitHub: JonPSmith, web: http://www.thereformedprogrammer.net/
// Licensed under MIT licence. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestSupport.DesignTimeServices;
using TestSupport.EfSchemeCompare.Internal;
using TestSupport.Helpers;

namespace TestSupport.EfSchemeCompare
{
    /// <summary>
    /// This is the main class for Comparing EF Core DbContexts against a database to see if they differ
    /// </summary>
    public class CompareEfSql
    {
        private readonly Assembly _callingAssembly;
        private readonly CompareEfSqlConfig _config;
        private readonly bool _forceStage2=false;
        private readonly List<CompareLog> _logs = new List<CompareLog>();

        /// <summary>
        /// This creates the comparer class that you use for comparing EF Core DbContexts to a database
        /// </summary>
        /// <param name="config"></param>
        /// <param name="forceStage2"></param>
        public CompareEfSql(CompareEfSqlConfig config = null, bool forceStage2=false)
        {
            _callingAssembly = Assembly.GetCallingAssembly();
            _config = config ?? new CompareEfSqlConfig();
            _forceStage2 = forceStage2;
        }

        /// <summary>
        /// This returns a single string containing all the errors found
        /// Each error is on a separate line
        /// </summary>
        public string GetAllErrors => string.Join(Environment.NewLine, CompareLog.ListAllErrors(Logs));
        public string GetAllErrorsHtml => string.Join("<br/>\n", CompareLog.ListAllErrorsHtml(Logs));

        /// <summary>
        /// This gives you access to the full log. but its not an easy thing to parse
        /// Look at the CompareLog class for various static methods that will output the log in a human-readable format
        /// </summary>
        public IReadOnlyList<CompareLog> Logs => _logs.ToImmutableList();

        /// <summary>
        /// This will compare one or more DbContext against database pointed to the first DbContext.
        /// </summary>
        /// <param name="dbContexts">One or more dbContext instances to be compared with the database</param>
        /// <returns>true if any errors found, otherwise false</returns>
        public bool CompareEfWithDb(params DbContext[] dbContexts)
        {
            if (dbContexts == null) throw new ArgumentNullException(nameof(dbContexts));
            if (dbContexts.Length == 0)
                throw new ArgumentException("You must provide at least one DbContext instance.", nameof(dbContexts));
            return CompareEfWithDb(dbContexts[0].Database.GetDbConnection().ConnectionString, dbContexts);
        }

        /// <summary>
        /// This will compare one or more DbContext against database pointed to the first DbContext
        /// using the DesignTimeServices type for T.
        /// </summary>
        /// <typeparam name="T">Must be the design time provider for the database provider you want to use, e.g. MySqlDesignTimeServices</typeparam>
        /// <param name="dbContexts">One or more dbContext instances to be compared with the database</param>
        /// <returns>true if any errors found, otherwise false</returns>
        public bool CompareEfWithDb<T>(params DbContext[] dbContexts) where T : IDesignTimeServices, new()
        {
            if (dbContexts == null) throw new ArgumentNullException(nameof(dbContexts));
            if (dbContexts.Length == 0)
                throw new ArgumentException("You must provide at least one DbContext instance.", nameof(dbContexts));
            var designTimeService = new T();
            return FinishRestOfCompare(dbContexts[0].Database.GetDbConnection().ConnectionString, dbContexts, designTimeService);
        }

        /// <summary>
        /// This will compare one or more DbContext against database pointed to by the configOrConnectionString.
        /// </summary>
        /// <param name="configOrConnectionString">This should either be a 
        /// connection string or the name of a connection string in the appsetting.json file.
        /// </param>
        /// <param name="dbContexts">One or more dbContext instances to be compared with the database</param>
        /// <returns>true if any errors found, otherwise false</returns>
        public bool CompareEfWithDb(string configOrConnectionString, params DbContext[] dbContexts )
        {
            if (dbContexts == null) throw new ArgumentNullException(nameof(dbContexts));
            if (dbContexts.Length == 0)
                throw new ArgumentException("You must provide at least one DbContext instance.", nameof(dbContexts));

            var designTimeService = dbContexts[0].GetDesignTimeService();
            return FinishRestOfCompare(configOrConnectionString, dbContexts, designTimeService);
        }


        /// <summary>
        /// This will compare one or more DbContext against database pointed to by the configOrConnectionString 
        /// using the DesignTimeServices type for T 
        /// </summary>
        /// <typeparam name="T">Must be the design time provider for the database provider you want to use, e.g. MySqlDesignTimeServices</typeparam>
        /// <param name="configOrConnectionString">This should either be a 
        /// connection string or the name of a connection string in the appsetting.json file.
        /// </param>
        /// <param name="dbContexts">One or more dbContext instances to be compared with the database</param>
        /// <returns>true if any errors found, otherwise false</returns>
        public bool CompareEfWithDb<T>(string configOrConnectionString, params DbContext[] dbContexts) where T: IDesignTimeServices, new()
        {
            if (configOrConnectionString == null) throw new ArgumentNullException(nameof(configOrConnectionString));
            if (dbContexts == null) throw new ArgumentNullException(nameof(dbContexts));
            if (dbContexts.Length == 0)
                throw new ArgumentException("You must provide at least one DbContext instance.", nameof(dbContexts));

            var designTimeService = new T();
            return FinishRestOfCompare(configOrConnectionString, dbContexts, designTimeService);
        }

        //------------------------------------------------------
        //private methods

        private bool FinishRestOfCompare(string configOrConnectionString, DbContext[] dbContexts, IDesignTimeServices designTimeService)
        {
            var databaseModel = GetDatabaseModelViaScaffolder(dbContexts, configOrConnectionString, designTimeService);
            bool hasErrors = false;
            foreach (var context in dbContexts)
            {
                var stage1Comparer = new Stage1Comparer(context.Model, context.GetType().Name, _config, _logs);
                hasErrors |= stage1Comparer.CompareModelToDatabase(databaseModel);
            }

            if (!_forceStage2 && hasErrors) return true;

            //No errors, so its worth running the second phase
            var stage2Comparer = new Stage2Comparer(databaseModel, _config);
            hasErrors = stage2Comparer.CompareLogsToDatabase(_logs);
            _logs.AddRange(stage2Comparer.Logs);
            return hasErrors;
        }

        private  DatabaseModel GetDatabaseModelViaScaffolder(DbContext[] contexts, string configOrConnectionString, IDesignTimeServices designTimeService)
        {
            var serviceProvider = designTimeService.GetDesignTimeProvider();
            var factory = serviceProvider.GetService<IDatabaseModelFactory>();
            var connectionString = configOrConnectionString == null
                ? contexts[0].Database.GetDbConnection().ConnectionString
                : GetConfigurationOrActualString(configOrConnectionString);

#if NETSTANDARD2_0
            var databaseModel = factory.Create(connectionString, new string[] { }, new string[] { });
#elif NETSTANDARD2_1
            var databaseModel = factory.Create(connectionString,
                new DatabaseModelFactoryOptions(new string[] { }, new string[] { }));
#endif
            RemoveAnyTableToIgnore(databaseModel, contexts);
            return databaseModel;
        }

        private void RemoveAnyTableToIgnore(DatabaseModel databaseModel, DbContext[] contexts)
        {

            var tablesToRemove = new List<DatabaseTable>();
            if (_config.TablesToIgnoreCommaDelimited == null)
            {
                //We remove all tables not mapped by the contexts
#if NETSTANDARD2_0
                var tablesInContext = contexts.SelectMany(x => x.Model.GetEntityTypes()).Where(x => !x.IsQueryType)
                    .Select(x => x.Relational().FormSchemaTable()).ToList();
#elif NETSTANDARD2_1
                var tablesInContext = contexts.SelectMany(x => x.Model.GetEntityTypes()).Where(x => x.FindPrimaryKey() != null)
                    .Select(x => x.FormSchemaTable()).ToList();
#endif
                tablesToRemove = databaseModel.Tables
                    .Where(x => !tablesInContext.Contains(x.FormSchemaTable(databaseModel.DefaultSchema), StringComparer.InvariantCultureIgnoreCase)).ToList();
            }
            else
            {
                
                foreach (var tableToIgnore in _config.TablesToIgnoreCommaDelimited.Split(',')
                    .Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x)))
                {
                    var split = tableToIgnore.Split('.').Select(x => x.Trim()).ToArray();
                    var schema = split.Length == 1 ? databaseModel.DefaultSchema : split[0];
                    var tableName = split.Length == 1 ? split[0] : split[1];
                    var tableToRemove = databaseModel.Tables
                        .SingleOrDefault(x => x.Schema.Equals(schema, StringComparison.InvariantCultureIgnoreCase)
                                           && x.Name.Equals(tableName, StringComparison.InvariantCultureIgnoreCase));
                    if (tableToRemove == null)
                        throw new InvalidOperationException(
                            $"The TablesToIgnoreCommaDelimited config property contains a table name of '{tableToIgnore}', which was not found in the database");
                    tablesToRemove.Add(tableToRemove);
                }
            }
            foreach (var tableToRemove in tablesToRemove)
            {
                databaseModel.Tables.Remove(tableToRemove);
            }
        }

        private string GetConfigurationOrActualString(string configOrConnectionString)
        {
            var config = AppSettings.GetConfiguration(_callingAssembly);
            var connectionFromConfigFile = config.GetConnectionString(configOrConnectionString);
            return connectionFromConfigFile ?? configOrConnectionString;
        }
    }
}