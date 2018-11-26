namespace DatabaseCopier.Models
{
    public class ForeignKey
    {
        public ForeignKey(int targetTableId, int ownerTableId)
        {
            TargetTableId = targetTableId;
            OwnerTableId = ownerTableId;
        }

        public int OwnerTableId { get; }

        public int TargetTableId { get; }
    }
}
