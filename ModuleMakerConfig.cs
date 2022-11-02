
namespace ModuleMakerConfig
{

    public class ModuleMakerConfig
    {
        public Module module { get; set; }
        public Configurator configurator { get; set; }
        public Genericlogger genericLogger { get; set; }
        public Processor processor { get; set; }
        public Subscriptor[] subscriptors { get; set; }
        public Deserializer[] deserializers { get; set; }
        public Serializer[] serializers { get; set; }
        public Publisher[] publishers { get; set; }
        public Subscriptors_Trees[] subscriptors_trees { get; set; }
        public Serializers_Trees[] serializers_trees { get; set; }
    }

    public class Module
    {
        public string id { get; set; }
        public string name { get; set; }
    }

    public class Configurator
    {
        public string libraryPath { get; set; }
        public string pathConfig { get; set; }
    }

    public class Genericlogger
    {
        public string libraryPath { get; set; }
        public string pathConfig { get; set; }
    }

    public class Processor
    {
        public string id { get; set; }
        public string name { get; set; }
        public string libraryPath { get; set; }
    }

    public class Subscriptor
    {
        public string id { get; set; }
        public string name { get; set; }
        public string libraryPath { get; set; }
    }

    public class Deserializer
    {
        public string id { get; set; }
        public string name { get; set; }
        public string libraryPath { get; set; }
    }

    public class Serializer
    {
        public string id { get; set; }
        public string name { get; set; }
        public string libraryPath { get; set; }
    }

    public class Publisher
    {
        public string id { get; set; }
        public string name { get; set; }
        public string libraryPath { get; set; }
    }

    public class Subscriptors_Trees
    {
        public string id { get; set; }
        public string[] deserializers_ids { get; set; }
    }

    public class Serializers_Trees
    {
        public string id { get; set; }
        public string[] publishers_ids { get; set; }
    }

}
