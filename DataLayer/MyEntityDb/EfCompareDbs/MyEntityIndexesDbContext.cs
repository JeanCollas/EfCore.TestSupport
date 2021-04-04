﻿// // Copyright (c) 2017 Jon P Smith, GitHub: JonPSmith, web: http://www.thereformedprogrammer.net/
// // Licensed under MIT licence. See License.txt in the project root for license information.

using System;
using DataLayer.MyEntityDb.ModelBuilders;
using Microsoft.EntityFrameworkCore;

namespace DataLayer.MyEntityDb.EfCompareDbs
{
    public class MyEntityIndexesDbContext : DbContext
    {
        public MyEntityIndexesDbContext(DbContextOptions<MyEntityIndexesDbContext> options)                            
            : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            AddMyEntity.Build(modelBuilder);
            modelBuilder.Entity<MyEntity>().HasIndex(p => p.MyInt).IsUnique().HasName("MySpecialName");
            modelBuilder.Entity<MyEntity>().HasIndex(p => p.MyString);
        }
    }
}