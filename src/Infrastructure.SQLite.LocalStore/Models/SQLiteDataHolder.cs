using SQLite;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.SQLite.LocalStore.Models;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public class SQLiteDataHolder
{
    [PrimaryKey]
    public string? Id { get; set; }

    public string? Group { get; set; }

    public string? Data { get; set; }
}
