using MessagePack;

namespace XRUIOS.Interfaces
{
    // The account DTO returned by the GetAccInfo capability.
    //
    // This used to be a MagicOnion [DataContract] carried by the IPublicAcc service.
    // The IPublicAcc service is gone: capabilities are now exposed as [SeaOfDirac]
    // methods and invoked over Eclipse's encrypted channel, where payloads are
    // serialized with MessagePack. Hence the [MessagePackObject] annotation.
    [MessagePackObject(keyAsPropertyName: true)]
    public struct PublicAccount
    {
        public string Name { get; set; }
        public string LastCheck { get; set; }
        public string OSFolder { get; set; }

        public PublicAccount(string name, string lastCheck, string oSFolder)
        {
            Name = name;
            LastCheck = lastCheck;
            OSFolder = oSFolder;
        }

        public override string ToString() =>
            $"PublicAccount(Name={Name}, OSFolder={OSFolder}, LastCheck={LastCheck})";
    }
}
