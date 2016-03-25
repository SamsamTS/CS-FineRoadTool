using ICities;

namespace FineRoadTool
{
    public class ModInfo : IUserMod
    {
        public string Name
        {
            get { return "Fine Road Tool " + version; }
        }

        public string Description
        {
            get { return "More road tool options"; }
        }

        public const string version = "0.4.1";
    }
}
