namespace DatabaseCopier.Models
{
    public class TableSchema
    {
        public int Id { get; private set; }

        public string Name { get; private set; }

        public TableSchema(int id, string name)
        {
            Name = name;
            Id = id;
        }

        public override string ToString() => Name;
    }
}
