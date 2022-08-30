﻿namespace UAssetAPI.AssetRegistry;

public enum EValueType
{
    AnsiString,
    WideString,
    NumberlessName,
    Name,
    NumberlessExportPath,
    ExportPath,
    LocalizedText
}

public class FValueId
{
    private const int _TYPE_BITS = 3;
    private static readonly int _INDEX_BITS = 32 - _TYPE_BITS;

    public readonly EValueType Type;
    public readonly int Index;
    
    public FValueId(FAssetRegistryReader ARreader)
    {
        var id = ARreader.ReadUInt32();
        Type = (EValueType) ((id << _INDEX_BITS) >> _INDEX_BITS);
        Index = (int)id >> _TYPE_BITS;
    }
}