﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UAssetAPI;
using UAssetAPI.FieldTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.PropertyTypes;
using UAssetAPI.StructTypes;
using static CookedAssetSerializer.KismetExpressionSerializer;
using static CookedAssetSerializer.Utils;

namespace CookedAssetSerializer {
	public static class SerializationUtils {

		public static JArray SerializeScript(FunctionExport function) {
			JArray jscript = new JArray();
			int index = 0;
			foreach (KismetExpression instruction in function.ScriptBytecode) {
				jscript.Add(SerializeExpression(instruction, ref index, true));
			}

			return jscript;
		}

		public static void CollectGeneratedVariables(ClassExport mainobject) {


			if (FindPropertyData(mainobject, "SimpleConstructionScript", out PropertyData scs)) {
				if (FindPropertyData(((ObjectPropertyData)scs).Value, "AllNodes", out PropertyData allnodes)) {
					ArrayPropertyData nodes = (ArrayPropertyData)allnodes;
					foreach (ObjectPropertyData node in nodes.Value) {
						if (FindPropertyData(node.Value, "InternalVariableName", out PropertyData property)) {
							GeneratedVariables.Add(property.ToString());
                        }
                    }	
				}
            }




        }


		public static JArray SerializeScript(KismetExpression[] code) {
			JArray jscript = new JArray();
			int index = 0;
			foreach (KismetExpression instruction in code) {
				jscript.Add(SerializeExpression(instruction, ref index, true));
			}

			return jscript;
		}

		public static JArray SerializeInterfaces(List<SerializedInterfaceReference> interfaces) {
			JArray jinterfaces = new JArray();
			foreach (SerializedInterfaceReference iinterface in interfaces) {
				JObject jinterface = new JObject();
				jinterface.Add("Class", Index(iinterface.Class));
				jinterface.Add("PointerOffset", iinterface.PointerOffset);
				jinterface.Add("bImplementedByK2", iinterface.bImplementedByK2);
				jinterfaces.Add(jinterface);
			}

			return jinterfaces;
		}

		public static JObject SerializeFunction(FunctionExport function, bool FieldKind = true) {
			//currentfunction = function;
			JObject jfunc = new JObject();
			if (FieldKind) { jfunc.Add("FieldKind", "Function"); }

			jfunc.Add("ObjectClass", asset.Imports[Math.Abs(function.TemplateIndex.Index) - 1].ClassName.ToName());
			jfunc.Add("ObjectName", function.ObjectName.ToName());
			jfunc.Add("SuperStruct", Index(function.SuperIndex.Index));
			jfunc.Add("Children", new JArray());
			JArray ChildProperties = new JArray();

			foreach (FProperty property in function.LoadedProperties) {
				ChildProperties.Add(SerializeProperty(property));
			}
			jfunc.Add("ChildProperties", ChildProperties);
			jfunc.Add("Script", SerializeScript(function));
			jfunc.Add("FunctionFlags", ((uint)function.FunctionFlags).ToString());

			return jfunc;
		}

		public static JObject SerializeProperty(FProperty property, bool FieldKind = true) {
			JObject jprop = new JObject();
			if (FieldKind) { jprop.Add("FieldKind", "Property"); }
			jprop.Add("ObjectClass", property.SerializedType.Value.Value);
			jprop.Add("ObjectName", property.Name.ToName());

			jprop.Add("ArrayDim", (byte)property.ArrayDim);
			jprop.Add("PropertyFlags", ((Int64)property.PropertyFlags).ToString());
			jprop.Add("RepNotifyFunc", property.RepNotifyFunc.Value.Value);
			jprop.Add("BlueprintReplicationCondition", ((byte)property.BlueprintReplicationCondition));

			switch (property) {
				case FEnumProperty fenum: {
						jprop.Add("Enum", Index(fenum.Enum.Index));
						jprop.Add("UnderlyingProp", SerializeProperty(fenum.UnderlyingProp, false));
						break;
					}
				case FArrayProperty farray: {
						jprop.Add("Inner", SerializeProperty(farray.Inner, false));
						break;
					};
				case FSetProperty fset: {
						jprop.Add("ElementType", SerializeProperty(fset.ElementProp, false));
						break;
					};
				case FMapProperty fmap: {
						jprop.Add("KeyProp", SerializeProperty(fmap.KeyProp, false));
						jprop.Add("ValueProp", SerializeProperty(fmap.ValueProp, false));
						break;
					};
				case FInterfaceProperty finterface: {
						jprop.Add("InterfaceClass", Index(finterface.InterfaceClass.Index));
						break;
					};
				case FBoolProperty fbool: {
						jprop.Add("BoolSize", fbool.ElementSize);
						jprop.Add("NativeBool", fbool.NativeBool);
						break;
					};
				case FByteProperty fbyte: {
						jprop.Add("Enum", Index(fbyte.Enum.Index));
						break;
					};
				case FStructProperty fstruct: {
						jprop.Add("Struct", Index(fstruct.Struct.Index));
						break;
					};
				case FNumericProperty fnumeric: { break; };
				case FGenericProperty fgeneric: { break; };

				case FSoftClassProperty fsoftclassprop: {
						jprop.Add("MetaClass", Index(fsoftclassprop.MetaClass.Index));
						break;
					};
				case FSoftObjectProperty fsoftobjprop: {
						jprop.Add("PropertyClass", Index(fsoftobjprop.PropertyClass.Index));
						break;
					};
				case FClassProperty fclassprop: {
						jprop.Add("MetaClass", Index(fclassprop.MetaClass.Index));
						break;
					};
				case FObjectProperty fobjprop: {
						jprop.Add("PropertyClass", Index(fobjprop.PropertyClass.Index));
						break;
					};

				//case FMulticastInlineDelegateProperty fmidelegate: {
				//        jprop.Add("SignatureFunction", Index(fmidelegate.SignatureFunction.Index));
				//        break; };
				//case FMulticastDelegateProperty fmdelegate: {
				//        jprop.Add("SignatureFunction", Index(fmdelegate.SignatureFunction.Index));
				//        break; };
				case FDelegateProperty fdelegate: {
						if (fdelegate.SignatureFunction.Index > 0) {
							jprop.Add("SignatureFunction", asset.Exports[fdelegate.SignatureFunction.Index - 1].ObjectName.ToName());
						} else if (fdelegate.SignatureFunction.Index < 0) {
							jprop.Add("SignatureFunction", asset.Imports[-fdelegate.SignatureFunction.Index - 1].ObjectName.ToName());
						} else {
							jprop.Add("SignatureFunction", -1);
						}
						break;
					};
				default:
					break;
			}


			return jprop;
		}

		public static JObject SerializaListOfProperties(List<PropertyData> Data, bool _struct = false) {

			if (!CheckDuplications(ref Data)) { }

			FName prev = null;
			JObject buffer1 = new JObject();
			JObject buffer2 = new JObject();

			JArray jproparray = new JArray();
			JProperty[] jpropvalue = null;

			foreach (PropertyData prop in Data) {
				if (!DisableGeneration.Contains(prop.Name.ToName())) {
					if (prop.Name == prev && prop.DuplicationIndex != 0) {

						jpropvalue = SerializePropertyData(prop);
						if (_struct) {
							if (jpropvalue.Length == 1) {
								jproparray.Add(jpropvalue[0].Value);
							} else {
								jproparray.Add(new JObject(jpropvalue));
							}
						} else {
							foreach (JProperty inprop in jpropvalue) {
								jproparray.Add(inprop.Value);
							}
						}
					} else {
						if (jproparray.Count > 1) {
							buffer2.Add(prev.ToName(), jproparray);
							buffer1 = (JObject)buffer2.DeepClone();
						} else {
							buffer2 = (JObject)buffer1.DeepClone();
						}
						jproparray = new JArray();
						jpropvalue = SerializePropertyData(prop);

						if (_struct) {
							if (jpropvalue.Length == 1) {
								jproparray.Add(jpropvalue[0].Value);
							} else {
								jproparray.Add(new JObject(jpropvalue));
							}

						} else {
							foreach (JProperty inprop in jpropvalue) {
								jproparray.Add(inprop.Value);
							}
						}
						buffer1.Add(jpropvalue);
					}
					prev = prop.Name;
				}
			}

			if (jproparray.Count > 1) {
				buffer2.Add(prev.ToName(), jproparray);
				return buffer2;
			} else {
				return buffer1;
			}

		}

		public static JObject SerializeNormalExport(NormalExport export, int index) {
			JObject jexport = new JObject();

			jexport.Add("ObjectIndex", index);
			jexport.Add("Type", "Export");
			jexport.Add("ObjectClass", Index(export.ClassIndex.Index));
			jexport.Add("Outer", Index(export.OuterIndex.Index));
			jexport.Add("ObjectName", export.ObjectName.ToName());
			jexport.Add("ObjectFlags", (Int64)export.ObjectFlags);
			JObject properties = SerializaListOfProperties(export.Data);
			properties.Add("$ReferencedObjects", JArray.FromObject(refobjects.Distinct<int>()));
			jexport.Add("Properties", properties);

			refobjects = new List<int>();
			return jexport;
		}


		public static JProperty[] SerializePropertyData(PropertyData property, bool withname = true) {
			JProperty jprop = new JProperty(property.Name.ToName());

			if (DisableGeneration.Contains(property.Name.ToName())){
				return null;
			}

			List<JProperty> res = new List<JProperty>();
			switch (property) {
				case BoolPropertyData:
				case FloatPropertyData:
				case DoublePropertyData:
				case Int8PropertyData:
				case Int16PropertyData:
				case IntPropertyData:
				case Int64PropertyData:
				case UInt16PropertyData:
				case UInt32PropertyData:
				case UInt64PropertyData:
				case GameplayTagContainerPropertyData:
				case TextPropertyData: { jprop.Value = property.ToJson(); res.Add(jprop); break; }
				case BytePropertyData prop: {
						if (prop.ByteType == BytePropertyType.Long) {
							jprop.Value = asset.GetNameReference(prop.Value).Value;

						} else {
							jprop.Value = prop.Value;
						}
						res.Add(jprop);
						break;
					}

				case EnumPropertyData prop: { jprop.Value = prop.Value.ToName(); res.Add(jprop); break; }
				case NamePropertyData prop: { jprop.Value = prop.Value.ToName(); res.Add(jprop); break; }
				case InterfacePropertyData prop: { jprop.Value = Index(prop.Value.Index); refobjects.Add((int)jprop.Value); res.Add(jprop); break; }
				case ObjectPropertyData prop: {
						int index = Index(prop.Value.Index);
						if (index == -1 && prop.Value.Index != 0) {
							if (prop.Value.ToExport(asset) is FunctionExport func) {
								jprop.Value = func.ObjectName.ToName(); res.Add(jprop);
							} else {
								Console.WriteLine("Non valid object index" + prop.Value.Index);
							}
						} else {
							jprop.Value = index; refobjects.Add(index); res.Add(jprop);
						}

						break;
					}
				case SoftObjectPropertyData prop: { jprop.Value = prop.Value.ToJson(); res.Add(jprop); break; }
				case StrPropertyData prop: {
						if (prop.Value != null) {
							jprop.Value = prop.Value.Value; res.Add(jprop);
						}
						break;
					}
				case MapPropertyData prop: {
						JArray jvaluearray = new JArray();
						for (var j = 1; j <= prop.Value.Count; j++) {
							JObject jobj = new JObject();

							JProperty key = SerializePropertyData(prop.Value.Keys.ElementAt(j - 1))[0];
							JProperty jkey = new JProperty("Key", key.Value);
							jobj.Add(jkey);

							key = SerializePropertyData(prop.Value.Values.ElementAt(j - 1))[0];
							jkey = new JProperty("Value", key.Value);
							jobj.Add(jkey);
							jvaluearray.Add(jobj);
						}
						jprop.Value = jvaluearray;
						res.Add(jprop);
						break;
					}
				//case SetPropertyData prop: { break; }
				case ArrayPropertyData prop: {
						JArray jvaluearray = new JArray();
						foreach (PropertyData valueelement in prop.Value) {
							JProperty[] element = SerializePropertyData(valueelement);
							foreach (JProperty ele in element) {
								jvaluearray.Add(ele.Value);
							}
						}
						jprop.Value = jvaluearray;
						res.Add(jprop);
						break;
					}
				case UnknownPropertyData prop: { jprop.Value = "##NOT SERIALIZED##"; res.Add(jprop); break; }
				case SmartNamePropertyData:
				case IntPointPropertyData:
				case GuidPropertyData:
				case ColorPropertyData:
				case LinearColorPropertyData:
				case RichCurveKeyPropertyData:
				case QuatPropertyData:
				case RotatorPropertyData:
				case Vector2DPropertyData:
				case Vector4PropertyData:
				case VectorPropertyData:
				case Box2DPropertyData:
				case SoftObjectPathPropertyData:
				case MovieSceneFrameRangePropertyData:
				case MovieSceneTrackIdentifierPropertyData:
				case MovieSceneSequenceIDPropertyData:
				case MovieSceneEvaluationKeyPropertyData:
				case MovieSceneEvaluationFieldEntityTreePropertyData:
				case MovieSceneEventParametersPropertyData:
				case MovieSceneSubSequenceTreePropertyData:
				case MovieSceneSegmentIdentifierPropertyData:
				case MovieSceneTrackFieldDataPropertyData:
				//case MovieSceneSequenceInstanceDataPtrPropertyData:
				case MovieSceneFloatChannelPropertyData: {
						res.AddRange(((JObject)property.ToJson()).Properties());
						break;
					}
				case FontDataPropertyData prop: {

						JObject value = new JObject();
						var fontdata = prop.Value;
						if (fontdata.bIsCooked) {
							value.Add("LocalFontFaceAsset", Index(fontdata.LocalFontFaceAsset.Index));
							if (fontdata.FontFilename != null) {
								value.Add("FontFilename", fontdata.FontFilename.ToString());
							} else {
								value.Add("FontFilename", null);
							}
							value.Add("Hinting", fontdata.Hinting.ToString());
							value.Add("LoadingPolicy", fontdata.LoadingPolicy.ToString());
							value.Add("SubFaceIndex", fontdata.SubFaceIndex);
						}

						jprop.Value = value;
						res.Add(jprop);
						break;
                    }
				case MovieSceneSegmentPropertyData prop: {
						JObject value = new JObject();
						value.Add("Range", prop.Value.Range.ToJson());
						value.Add("ID", prop.Value.ID.IdentifierIndex);
						value.Add("bAllowEmpty", prop.Value.bAllowEmpty);

						JArray jimpls = new JArray();


						foreach (List<PropertyData> item in prop.Value.Impls) {
							JObject structres = SerializaListOfProperties(item, true);
							jimpls.Add(structres);
						}

						JObject data = new JObject();
						value.Add("Impls", jimpls);
						jprop.Value = value;
						res.Add(jprop);
						break;
					}
				case SectionEvaluationDataTreePropertyData prop: {
						TMovieSceneEvaluationTree<List<PropertyData>> Tree = prop.Value.Tree;

						JObject value = new JObject();

						JObject serdata = new JObject();
						serdata.Add("RootNode", Tree.RootNode.ToJson());

						JArray entries = new JArray();
						JArray items = new JArray();
						foreach (FEntry entry in Tree.ChildNodes.Entries) {
							entries.Add(entry.ToJson());
						}
						foreach (FMovieSceneEvaluationTreeNode item in Tree.ChildNodes.Items) {
							items.Add(item.ToJson());
						}
						JObject childnodes = new JObject();

						childnodes.Add("Entries", entries);
						childnodes.Add("Items", items);
						serdata.Add("ChildNodes", childnodes);

						entries = new JArray();
						items = new JArray();
						foreach (FEntry entry in Tree.Data.Entries) {
							entries.Add(entry.ToJson());
						}


						foreach (List<PropertyData> item in Tree.Data.Items) {
							JObject structres = SerializaListOfProperties(item, true);
							items.Add(structres);
						}

						JObject data = new JObject();
						data.Add("Entries", entries);
						data.Add("Items", items);
						serdata.Add("Data", data);
						value.Add("Tree", serdata);
						jprop.Value = value;
						res.Add(jprop);
						break;
					}
				case StructPropertyData prop: {
						JObject structres = SerializaListOfProperties(prop.Value, true);
						jprop.Value = structres;
						res.Add(jprop);
						break;
					}
				case FieldPathPropertyData prop: {
						if (prop.Value.Length == 0) {
							jprop.Value = "##NOT SERIALIZED##"; res.Add(jprop); break;
						} else {
							if (prop.Value.Length > 1) { Console.WriteLine("FieldPathPropertyData Values array has more than one name"); }
							jprop.Value = GetFullName(prop.ResolvedOwner.Index) + ":" + prop.Value[0].ToName(); res.Add(jprop); break;
						}
					}
				default: {
						Console.WriteLine(property.PropertyType.ToName());
						jprop.Value = "##NOT SERIALIZED##"; res.Add(jprop); break;
					}

					//case MulticastDelegatePropertyData prop: { break; }
					//case BoxPropertyData prop: { break; }
					//case DateTimePropertyData prop: {break; 
					//case ExpressionInputPropertyData prop: { break; }
					//case MaterialAttributesInputPropertyData prop: { break; }
					//case ColorMaterialInputPropertyData prop: { break; }
					//case ScalarMaterialInputPropertyData prop: { break; }
					//case ShadingModelMaterialInputPropertyData prop: { break; }
					//case VectorMaterialInputPropertyData prop: { break; }
					//case Vector2MaterialInputPropertyData prop: { break; }
					//case PerPlatformBoolPropertyData prop: { break; }
					//case PerPlatformFloatPropertyData prop: { break; }
					//case PerPlatformIntPropertyData prop: { break; }
					//case SkeletalMeshAreaWeightedTriangleSamplerPropertyData prop: { break; }
					//case SkeletalMeshSamplingLODBuiltDataPropertyData prop: { break; }
					//case SoftAssetPathPropertyData prop: { break; }
					//case SoftClassPathPropertyData prop: { break; }
					//case TimespanPropertyData prop: {break; }
					//case ViewTargetBlendParamsPropertyData prop: { break; }
					//case WeightedRandomSamplerPropertyData prop: { break; }
					//case DelegatePropertyData prop: {
					//	Console.WriteLine(prop.Name.ToName());
					//	break; }

			}

			return res.ToArray();
		}


		public static JProperty SerializeData(List<PropertyData> Data, bool mainobject = true) {
			JProperty jdata;
			refobjects = new List<int>();

			if (mainobject) {
				jdata = new JProperty("AssetObjectData");

			} else { jdata = new JProperty("Properties"); }

			if (Data.Count > 0) {
				JObject jdatavalue = new JObject();
				foreach (PropertyData property in Data) {
					jdatavalue.Add(SerializePropertyData(property));
				}
				if (mainobject) {
					bool hasSCS = false;
					foreach (JProperty jprop in jdatavalue.Properties()) {
						if (jprop.Name == "SimpleConstructionScript") { 
							hasSCS = true;
							break; 
						}
					}
					if (!hasSCS) { jdatavalue.Add("SimpleConstructionScript", -1); }
				}
				jdatavalue.Add("$ReferencedObjects", JArray.FromObject(refobjects.Distinct<int>()));
				refobjects = new List<int>();
				jdata.Value = jdatavalue;

				return jdata;
			} else {
				return jdata;
			}
		}

		public static JProperty ObjectHierarchy(UAsset asset) {
			JArray ObjHie = new JArray();
			for (var i = 1; i <= asset.Imports.Count; i++) {
				if (dict.ContainsKey(-i)) {
					Import import = asset.Imports[i - 1];
					JObject jimport = new JObject();
					jimport.Add("ObjectIndex", Index(-i));
					jimport.Add("Type", "Import");
					jimport.Add("ClassPackage", import.ClassPackage.Value.Value);
					jimport.Add("ClassName", import.ClassName.ToName());
					if (import.OuterIndex.Index != 0) {
						jimport.Add("Outer", Index(import.OuterIndex.Index));
					}
					jimport.Add("ObjectName", import.ObjectName.Value.Value);
					ObjHie.Add(jimport);
				}

			}

			for (var i = 1; i <= asset.Exports.Count; i++) {
				if (dict.ContainsKey(i)) {
					JObject jexport = new JObject();
					if (i == asset.mainExport) {
						jexport.Add("ObjectIndex", Index(i));
						jexport.Add("Type", "Export");
						jexport.Add("ObjectMark", "$AssetObject$");
					} else {

						switch (asset.Exports[i - 1]) {
							case FunctionExport function: { break; }

							case NormalExport normal: {
									jexport = SerializeNormalExport(normal, Index(i));
									break;
								}
							default: break;
						}
					}
					ObjHie.Add(jexport);
				}
			}

			return new JProperty("ObjectHierarchy", ObjHie);
		}



	}
}
