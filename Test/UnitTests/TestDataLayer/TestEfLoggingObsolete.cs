﻿// Copyright (c) 2017 Jon P Smith, GitHub: JonPSmith, web: http://www.thereformedprogrammer.net/
// Licensed under MIT licence. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using DataLayer.BookApp;
using DataLayer.EfCode.BookApp;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Test.Helpers;
using TestSupport.Attributes;
using TestSupport.EfHelpers;
using Xunit;
using Xunit.Abstractions;
using Xunit.Extensions.AssertExtensions;

namespace Test.UnitTests.TestDataLayer
{
    public class TestEfLoggingObsolete
    {
        private readonly ITestOutputHelper _output; //#A

        public TestEfLoggingObsolete(ITestOutputHelper output) //#B
        {
            _output = output;
        }

        [Fact]
        public void TestEfCoreLoggingExample()
        {
            //SETUP
            var options = SqliteInMemory
                .CreateOptions<BookContext>();
            using (var context = new BookContext(options))
            {
                context.Database.EnsureCreated();
                context.SeedDatabaseFourBooks();
                var logs = context.SetupLogging(); //#C

                //ATTEMPT
                var books = context.Books.ToList(); //#D

                //VERIFY
                foreach (var log in logs.ToList()) //This stops the 'bleed' problem
                {                                     //#E
                    _output.WriteLine(log.ToString());//#E
                }                                     //#E
            }
        }
        /***********************************************************
        #A In xUnit, which runs in parallel, I need to use the ITestOutputHelper to output to the unit test runner
        #B The ITestOutputHelper is injected by the xUnit test runner
        #C Here I set up the logging, which returns a reference to a list of LogOutput classes. This contains separate properties for the LogLevel, EventId, Message and so on
        #D This is the query that I want to log
        #E This outputs the logged data
         * *********************************************************/

        [Fact]
        public void TestEfCoreLoggingStringWithBadValues()
        {
            //SETUP
            var options = SqliteInMemory.CreateOptions<BookContext>();
            using (var context = new BookContext(options))
            {
                context.Database.EnsureCreated();

                //ATTEMPT
                var logs = context.SetupLogging();
                context.Books.Count();
                context.Add(new Book {Title = "The person's boss said, \"What's that about?\""});
                context.SaveChanges();

                //VERIFY
                foreach (var log in logs.ToList()) //This stops the 'bleed' problem
                {
                    _output.WriteLine(log.ToString());
                }
            }
        }

        [Fact]
        public void TestEfCoreLogging1()
        {
            //SETUP
            var options = SqliteInMemory.CreateOptions<BookContext>();
            using (var context = new BookContext(options))
            {
                //ATTEMPT
                var logs = context.SetupLogging();
                context.Database.EnsureCreated();

                //VERIFY
                foreach (var log in logs.ToList()) //This stops the 'bleed' problem
                {
                    _output.WriteLine(log.ToString());
                }
                logs.Count.ShouldBeInRange(11, 50);
            }
        }

        [Fact]
        public void TestEfCoreLogging2()
        {
            //SETUP
            var options = SqliteInMemory.CreateOptions<BookContext>();
            using (var context = new BookContext(options))
            {
                //ATTEMPT
                var logs = context.SetupLogging();
                context.Database.EnsureCreated();

                //VERIFY
                logs.Count.ShouldBeInRange(11, 50);
            }
        }

        [Fact]
        public void TestEfCoreLoggingWithMutipleDbContexts()
        {
            //SETUP
            List<LogOutput> logs1;
            var options1 = SqliteInMemory.CreateOptions<BookContext>();
            using (var context = new BookContext(options1))
            {
                //ATTEMPT
                logs1 = context.SetupLogging();
                context.Database.EnsureCreated();
            }
            var logs1Count = logs1.Count;
            var options2 = SqliteInMemory.CreateOptions<BookContext>();
            using (var context = new BookContext(options2))
            {
                //ATTEMPT
                var logs = context.SetupLogging();
                context.Database.EnsureCreated();

                //VERIFY
                logs.Count.ShouldBeInRange(1, 100);
                logs1.Count.ShouldNotEqual(logs1Count); //The second DbContext methods are also logged to the first logger
            }
        }

        private class ClientSeverTestDto
        {
            public string ClientSideProp { get; set; }
        }

        [Fact]
        public void TestLogQueryClientEvaluationWarning()
        {
            //SETUP
            var options = SqliteInMemory.CreateOptions<BookContext>(false);
            using (var context = new BookContext(options))
            {
                context.Database.EnsureCreated();
                var logs = context.SetupLogging();

                //ATTEMPT
                var books = context.Books.Select(x => new ClientSeverTestDto
                {
                    ClientSideProp = x.Price.ToString("C")
                }).OrderBy(x => x.ClientSideProp)
                .ToList();

                //VERIFY
                logs.ToList().Any(x => x.EventId.Name == RelationalEventId.QueryClientEvaluationWarning.Name).ShouldBeTrue();
            }
        }

        [RunnableInDebugOnly] //#A
        public void CaptureSqlEfCoreCreatesDatabase()
        {
            //SETUP
            var options = this.
                CreateUniqueClassOptions<BookContext>();
            using (var context = new BookContext(options))
            {
                var logs = context.SetupLogging(); //#B

                //ATTEMPT
                context.Database.EnsureDeleted(); //#C
                context.Database.EnsureCreated(); //#C

                //VERIFY
                foreach (var log in logs.ToList()) //This stops the 'bleed' problem
                {                                     //#D
                    _output.WriteLine(log.Message);   //#D
                }                                     //#D
            }
        }
        /*************************************************************************
        #A I don't need this to run every time, so I add the RunnableInDebugOnly attribute so that it doesn't get run in the normal unit test run
        #B I set up the logging before the database is created
        #C This combination ensures a new database is created that matches the current EF Core's database Model
        #D I output only the Message part of the logging, so that I can cut-and-paste the SQL out of the logged data
         * *********************************************************************/
    }
}