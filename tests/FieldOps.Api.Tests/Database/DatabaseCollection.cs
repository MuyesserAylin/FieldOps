using System;

namespace FieldOps.Api.Tests.Database;

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<PostgisFixture>
{

}
