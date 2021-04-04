﻿// Copyright (c) 2017 Jon P Smith, GitHub: JonPSmith, web: http://www.thereformedprogrammer.net/
// Licensed under MIT licence. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using DataLayer.BookApp;
using DataLayer.EfCode.BookApp;
using DataLayer.SpecialisedEntities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Test.Helpers;
using TestSupport.Attributes;
using TestSupport.EfHelpers;
using Xunit;
using Xunit.Abstractions;
using Xunit.Extensions.AssertExtensions;

namespace Test.UnitTests.TestDataLayer
{
    public class TestEfLoggingDecodeBookContext
    {
        private readonly ITestOutputHelper _output; 

        public TestEfLoggingDecodeBookContext(ITestOutputHelper output) 
        {
            _output = output;
        }

        [Fact]
        public void TestDecodeMessageNotSensitiveLogging()
        {
            //SETUP
            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();


            var logs = new List<LogOutput>();
            var options = new DbContextOptionsBuilder<BookContext>()
                .UseLoggerFactory(new LoggerFactory(new[] { new MyLoggerProviderActionOut(l => logs.Add(l))}))
                .UseSqlite(connection)
                //.EnableSensitiveDataLogging()
                .Options;
            using (var context = new BookContext(options))
            {
                context.Database.EnsureCreated();
                context.SeedDatabaseFourBooks();

                //ATTEMPT
                var id = context.Books.First().BookId;
                var book = context.Books.Single(x => x.BookId == id);
                var decoded = logs.Last().DecodeMessage();

                //VERIFY
                var sqlCommand = decoded.Split('\n').Skip(1).Select(x => x.Trim()).ToArray();
                sqlCommand[0].ShouldEqual(
                    "SELECT \"p\".\"BookId\", \"p\".\"Description\", \"p\".\"ImageUrl\", \"p\".\"Price\", \"p\".\"PublishedOn\", \"p\".\"Publisher\", \"p\".\"SoftDeleted\", \"p\".\"Title\"");
                sqlCommand[1].ShouldEqual("FROM \"Books\" AS \"p\"");
                sqlCommand[2].ShouldEqual("WHERE (\"p\".\"SoftDeleted\" = 0) AND (\"p\".\"BookId\" = @__id_0)");
            }
        }

        [Fact]
        public void TestDecodeMessageNoParams()
        {
            //SETUP
            var logs = new List<LogOutput>();
            var options = SqliteInMemory.CreateOptionsWithLogging<BookContext>(log => logs.Add(log));
            //var options = this.CreateUniqueClassOptionsWithLogging<BookContext>(log => logs.Add(log));
            using (var context = new BookContext(options))
            {
                context.Database.EnsureCreated();
                context.SeedDatabaseFourBooks();

                //ATTEMPT
                context.Books.Count();
                var decoded = logs.Last().DecodeMessage();

                //VERIFY  
                var sqlCommand = decoded.Split('\n').Skip(1).Select(x => x.Trim()).ToArray();
                sqlCommand[0].ShouldEqual(
                    "SELECT COUNT(*)");
                sqlCommand[1].ShouldEqual("FROM \"Books\" AS \"p\"");
                sqlCommand[2].ShouldEqual("WHERE \"p\".\"SoftDeleted\" = 0");
            }
        }

        [Fact]
        public void TestDecodeMessageOneParams()
        {
            //SETUP
            var logs = new List<LogOutput>();
            //var options = SqliteInMemory.CreateOptionsWithLogging<BookContext>(log => logs.Add(log));
            var options = this.CreateUniqueClassOptionsWithLogging<BookContext>(log => logs.Add(log));
            using (var context = new BookContext(options))
            {
                context.Database.EnsureCreated();
                context.SeedDatabaseFourBooks();

                //ATTEMPT
                var id = context.Books.First().BookId;
                var book = context.Books.Single(x => x.BookId == id);
                var decoded = logs.Last().DecodeMessage();

                //VERIFY  
                var sqlCommand = decoded.Split('\n').Skip(1).Select(x => x.Trim()).ToArray();
                sqlCommand[0].ShouldEqual(
                        "SELECT TOP(2) [p].[BookId], [p].[Description], [p].[ImageUrl], [p].[Price], [p].[PublishedOn], [p].[Publisher], [p].[SoftDeleted], [p].[Title]");
                sqlCommand[1].ShouldEqual("FROM [Books] AS [p]");
                sqlCommand[2].ShouldEqual("WHERE ([p].[SoftDeleted] = 0) AND ([p].[BookId] = '1')");
            }
        }



    }
}