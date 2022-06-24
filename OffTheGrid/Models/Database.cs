using Bogus;
using Microsoft.EntityFrameworkCore;

namespace OffTheGrid.Models;

public class Database : DbContext
{
    public ILogger<Database> Logger { get; }

    public Database(ILogger<Database> logger)
    {
        Logger = logger;
    }
    
    public DbSet<Person> People { get; set; } = default!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder
            .UseSqlite("Data Source=database.db")
            .LogTo(m => Logger.LogInformation(m), 
                (id, _) => id.Name?.Contains("CommandExecuted") == true);

    public static void Initialize(WebApplication app, int count = 1_000)
    {
        using var scope = app.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<Database>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Database>>();
        
        // migrate the database if we haven't already
        database.Database.Migrate();

        if (database.People.Any())
        {
            logger.LogInformation("Database already initialized");
            return;
        }

        var generator = new Faker<Person>()
            //.RuleFor(m => m.Id, (f, _) => f.IndexFaker)
            .RuleFor(m => m.Name, f => f.Name.FullName())
            .RuleFor(m => m.Hobby, f => f.Commerce.Department())
            .RuleFor(m => m.Age, f => f.Finance.Random.Number(16, 89));

        var chunks = Enumerable.Range(1, count).Chunk(100).Select((v, i) => (Index: i, Value: v.Length)).ToList();
        logger.LogInformation("{ChunkCount} of Chunks To Initialize", chunks.Count);
        foreach (var chunk in chunks)
        {
            logger.LogInformation("#{Index}: Generating {Chunk} rows of People", chunk.Index, chunk.Value);
            var records = generator.Generate(chunk.Value);
            database.People.AddRange(records);
            database.SaveChanges();
            database.ChangeTracker.Clear();
        }
    }
}

public class Person
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; } = 0;
    public string Hobby { get; set; } = string.Empty;
}