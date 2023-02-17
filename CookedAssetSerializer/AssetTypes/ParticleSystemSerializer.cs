using Newtonsoft.Json.Schema;
using System.Security.Cryptography;
using UAssetAPI.UE4;

namespace CookedAssetSerializer.AssetTypes;

public class ParticleSystemSerializer : SimpleAssetSerializer<NormalExport>
{
    public ParticleSystemSerializer(JSONSettings settings, UAsset asset) : base(settings, asset)
    {
        Settings = settings;
        Asset = asset;
    }

}