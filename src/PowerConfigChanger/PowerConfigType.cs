
namespace PowerConfigChanger
{
    public class PowerConfigType
    {
        public PowerConfigType(int no, string id, string name, bool isCurrent)
        {
            NO = no;
            ID = id;
            Name = name;
            IsCurrent = isCurrent;
        }
        public int NO { get; }
        public string ID { get; }
        public string Name { get; }

        public bool IsCurrent { get; }
        public override string ToString()
        {
            return $"{NO}:{Name}:{ID}:{IsCurrent}";
        }
    }
}
