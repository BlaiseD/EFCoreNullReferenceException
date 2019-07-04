using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Xunit;

namespace ConditionalFilterError
{
    public class UnitTest
    {
        public UnitTest()
        {
            Initialize();
        }

        [Fact]
        public void Building_expand_Builder_Tenant_expand_City_filter_on_nested_nested_property_and_order_by_tData()
        {
            ParameterExpression userParam = Expression.Parameter(typeof(TBuilding), "s");
            MemberExpression builderProperty = Expression.MakeMemberAccess(userParam, GetMemberInfo(typeof(TBuilding), "Builder"));
            MemberExpression cityProperty = Expression.MakeMemberAccess(builderProperty, GetMemberInfo(typeof(TBuilder), "City"));
            MemberExpression nameProperty = Expression.MakeMemberAccess(cityProperty, GetMemberInfo(typeof(TCity), "Name"));

            //{s => (IIF((IIF((s.Builder == null), null, s.Builder.City) == null), null, s.Builder.City.Name) == "Leeds")}
            Expression<Func<TBuilding, bool>> selection = Expression.Lambda<Func<TBuilding, bool>>
            (
                Expression.Equal
                (
                    Expression.Condition
                    (
                        Expression.Equal
                        (
                            Expression.Condition
                            (
                                Expression.Equal
                                (
                                    builderProperty,
                                    Expression.Constant(null, typeof(TBuilder))
                                ),
                                Expression.Constant(null, typeof(TCity)),
                                cityProperty
                            ),
                            Expression.Constant(null, typeof(TCity))
                        ),
                        Expression.Constant(null, typeof(string)),
                        nameProperty
                    ),
                    Expression.Constant("Leeds", typeof(string))
                ),
                userParam
            );

            MyDbContext context = serviceProvider.GetRequiredService<MyDbContext>();

            IQueryable<TBuilding> query = context.BuildingSet;

            Test
            (
                query.Where(selection)
                    .Include(a => a.Builder).ThenInclude(a => a.City)
                    .Include(a => a.Mandator).ToList()
            );

            void Test(ICollection<TBuilding> collection)
            {
                Assert.True(collection.Count == 1);
                Assert.True(collection.First().Builder.City.Name == "Leeds");
                Assert.True(collection.First().LongName == "Two L2");
            }
        }

        private MemberInfo GetMemberInfo(Type parentType, string memberName)
        {
            MemberInfo mInfo = parentType.GetMember(memberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.IgnoreCase).FirstOrDefault();
            if (mInfo == null)
                throw new ArgumentException($"Member {memberName} does not exist.");

            return mInfo;
        }

        private void Initialize()
        {
            IServiceCollection services = new ServiceCollection();
            services.AddDbContext<MyDbContext>
                (
                    options =>
                    {
                        options.UseInMemoryDatabase("MyDbContext");
                        options.UseInternalServiceProvider(new ServiceCollection().AddEntityFrameworkInMemoryDatabase().BuildServiceProvider());
                    }
                );

            serviceProvider = services.BuildServiceProvider();

            MyDbContext context = serviceProvider.GetRequiredService<MyDbContext>();
            context.Database.EnsureCreated();
            Seed_Database(context);
        }

        static void Seed_Database(MyDbContext context)
        {
            context.City.Add(new TCity { Name = "London" });
            context.City.Add(new TCity { Name = "Leeds" });
            context.SaveChanges();

            List<TCity> cities = context.City.ToList();
            context.Builder.Add(new TBuilder { Name = "Sam", CityId = cities.First(b => b.Name == "London").Id });
            context.Builder.Add(new TBuilder { Name = "John", CityId = cities.First(b => b.Name == "London").Id });
            context.Builder.Add(new TBuilder { Name = "Mark", CityId = cities.First(b => b.Name == "Leeds").Id });
            context.SaveChanges();

            List<TBuilder> builders = context.Builder.ToList();
            context.MandatorSet.Add(new TMandator
            {
                Identity = Guid.NewGuid(),
                Name = "One",
                Buildings = new List<TBuilding>
                    {
                        new TBuilding { Identity =  Guid.NewGuid(), LongName = "One L1", BuilderId = builders.First(b => b.Name == "Sam").Id },
                        new TBuilding { Identity =  Guid.NewGuid(), LongName = "One L2", BuilderId = builders.First(b => b.Name == "Sam").Id  }
                    }
            });
            context.MandatorSet.Add(new TMandator
            {
                Identity = Guid.NewGuid(),
                Name = "Two",
                Buildings = new List<TBuilding>
                    {
                        new TBuilding { Identity =  Guid.NewGuid(), LongName = "Two L1", BuilderId = builders.First(b => b.Name == "John").Id  },
                        new TBuilding { Identity =  Guid.NewGuid(), LongName = "Two L2", BuilderId = builders.First(b => b.Name == "Mark").Id  }
                    }
            });
            context.SaveChanges();
        }

        private IServiceProvider serviceProvider;
    }

    public class MyDbContext : DbContext
    {
        public MyDbContext(DbContextOptions<MyDbContext> options) : base(options)
        {
            this.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            this.ChangeTracker.AutoDetectChangesEnabled = false;
        }

        public DbSet<TMandator> MandatorSet { get; set; }
        public DbSet<TBuilding> BuildingSet { get; set; }
        public DbSet<TBuilder> Builder { get; set; }
        public DbSet<TCity> City { get; set; }
    }

    [Table("OB_TBuilding")]
    public class TBuilding
    {

        [Column("pkBID")]
        [Required, Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Int32 Id { get; set; }

        [Column("Identifier")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Identity { get; set; }

        [Column("sLongName")]
        public String LongName { get; set; }

        [ForeignKey("Builder")]
        public Int32 BuilderId { get; set; }

        public TBuilder Builder { get; set; }

        public TMandator Mandator { get; set; }

        [ForeignKey("Mandator")]
        [Column("fkMandatorID")]
        public Int32 MandatorId { get; set; }
    }

    [Table("TBuilders")]
    public class TBuilder
    {
        [Column("Id")]
        [Required, Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Int32 Id { get; set; }

        [Column("Name")]
        public String Name { get; set; }

        [ForeignKey("City")]
        public Int32 CityId { get; set; }

        public TCity City { get; set; }
    }

    [Table("TCities")]
    public class TCity
    {
        [Column("Id")]
        [Required, Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Int32 Id { get; set; }

        [Column("Name")]
        public String Name { get; set; }
    }

    [Table("G_TMandator")]
    public class TMandator
    {
        [Column("pkMandatorID")]
        [Required, Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Int32 Id { get; set; }

        [Column("gIdentity")]
        public Guid Identity { get; set; }

        [Column("sName")]
        public String Name { get; set; }
        public virtual ICollection<TBuilding> Buildings { get; set; }
    }
}
