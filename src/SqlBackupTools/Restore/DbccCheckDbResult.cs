namespace SqlBackupTools.Restore
{
    public class DbccCheckDbResult
    {
        public int Error { get; set; }
        public int Level { get; set; }
        public int State { get; set; }
        public string MessageText { get; set; }
        public int RepairLevel { get; set; }
        public int Status { get; set; }
        public int DbId { get; set; }
        public int DbFragId { get; set; }
        public int ObjectId { get; set; }
        public int IndexId { get; set; }
        public int PartitionID { get; set; }
        public int AllocUnitID { get; set; }
        public int RidDbId { get; set; }
        public int RidPruId { get; set; }
        public int File { get; set; }
        public int Page { get; set; }
        public int Slot { get; set; }
        public int RefDbId { get; set; }
        public int RefPruId { get; set; }
        public int RefFile { get; set; }
        public int RefPage { get; set; }
        public int RefSlot { get; set; }
        public int Allocation { get; set; }
    }
}
