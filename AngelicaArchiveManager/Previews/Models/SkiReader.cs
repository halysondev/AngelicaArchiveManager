using AngelicaArchiveManager.Core.ArchiveEngine;
using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using AngelicaArchiveManager.Previews.Models.Extensions;
using System.Threading.Tasks;

namespace AngelicaArchiveManager.Previews.Models
{
	public class SkiReader
	{
        public IArchiveManager Manager { get; set; }

		public string[] Bips;

		public string[] Textures;

		public string ModelFilePath
		{
			get;
			set;
		}

		public byte[] Signature
		{
			get;
			set;
		}

		public uint SkiType
		{
			get;
			set;
		}

		public uint[] MeshCount
		{
			get;
			set;
		}

		public uint TexturesCount
		{
			get;
			set;
		}

		public uint MaterialsCount
		{
			get;
			set;
		}

		public uint NumBips
		{
			get;
			set;
		}

		public uint Unknow2
		{
			get;
			set;
		}

		public uint TypeMask
		{
			get;
			set;
		}

		public byte[] UnknowBytes
		{
			get;
			set;
		}

		public SkiMaterial[] Materials
		{
			get;
			set;
		}

		public MeshObject[] Object
		{
			get;
			set;
		}

		public SkiReader(byte[] file)
		{
			this.Bips = new string[0];
			this.Textures = new string[0];
			using (BinaryReader binaryReader = new BinaryReader(new MemoryStream(file)))
			{
				this.Signature = binaryReader.ReadBytes(8);
				if (this.Signature[0] == 77)
				{
					if (this.Signature[7] == 65)
					{
						this.SkiType = binaryReader.ReadUInt32();
						this.MeshCount = new uint[4];
						for (int i = 0; i < 4; i++)
						{
							this.MeshCount[i] = binaryReader.ReadUInt32();
						}
						this.TexturesCount = binaryReader.ReadUInt32();
						this.MaterialsCount = binaryReader.ReadUInt32();
						this.NumBips = binaryReader.ReadUInt32();
						this.Unknow2 = binaryReader.ReadUInt32();
						this.TypeMask = binaryReader.ReadUInt32();
						this.UnknowBytes = binaryReader.ReadBytes(60);
						if (this.SkiType == 9u)
						{
							this.Bips = new string[this.NumBips];
							int num = 0;
							while ((long)num < (long)((ulong)this.NumBips))
							{
								int count = binaryReader.ReadInt32();
								byte[] bytes = binaryReader.ReadBytes(count);
								this.Bips[num] = Encoding.GetEncoding("GB2312").GetString(bytes);
								num++;
							}
						}
						this.Textures = new string[this.TexturesCount];
						int num2 = 0;
						while ((long)num2 < (long)((ulong)this.TexturesCount))
						{
							int count2 = binaryReader.ReadInt32();
							byte[] bytes2 = binaryReader.ReadBytes(count2);
							this.Textures[num2] = Encoding.GetEncoding("GB2312").GetString(bytes2).Replace(".DDS", ".dds");
							num2++;
						}
						this.Materials = new SkiMaterial[this.MaterialsCount];
						int num3 = 0;
						while ((long)num3 < (long)((ulong)this.MaterialsCount))
						{
							this.Materials[num3] = SkiMaterial.Read(binaryReader);
							num3++;
						}
						if (this.MeshCount[0] != 0u)
						{
							this.Object = new MeshObject[this.MeshCount[0]];
							int num4 = 0;
							while ((long)num4 < (long)((ulong)this.MeshCount[0]))
							{
								this.Object[num4] = MeshObject.Read(binaryReader, 0);
								num4++;
							}
						}
						else
						{
							this.Object = new MeshObject[1];
							this.Object[0] = MeshObject.Read(binaryReader, 1);
						}
						return;
					}
				}
				throw new Exception("Its no ski format");
			}
		}

		private Material GetTexture(string name)
		{
            var texture = Manager.Files.Where(x => x.Path.StartsWith(ModelFilePath.RemoveFirstSeparator()) && x.Path.EndsWith(name));
            if (texture.Count() > 0)
                return MaterialHelper.CreateImageMaterial(Manager.GetFile(texture.First()).ToImage(), 1.0);
			return HelixToolkit.Wpf.Materials.Gray;
		}

        private async Task<Material> GetTextureAsync(string name)
        {
            var texture = Manager.Files.Where(x => x.Path.StartsWith(ModelFilePath.RemoveFirstSeparator()) && x.Path.EndsWith(name));
            if (texture.Count() > 0)
            {
                byte[] textureData = await Manager.GetFileAsync(texture.First());
                return MaterialHelper.CreateImageMaterial(textureData.ToImage(), 1.0);
            }
            return HelixToolkit.Wpf.Materials.Gray;
        }

		public Model3DGroup GetModel()
		{
			Model3DGroup model3DGroup = new Model3DGroup();
			MeshObject[] @object = this.Object;
			for (int i = 0; i < @object.Length; i++)
			{
				MeshObject meshObject = @object[i];
				List<Point3D> list = new List<Point3D>();
				List<Vector3D> list2 = new List<Vector3D>();
				List<Point> list3 = new List<Point>();
				Vertex[] vertexes = meshObject.Vertexes;
				for (int j = 0; j < vertexes.Length; j++)
				{
					Vertex vertex = vertexes[j];
					list.Add(vertex.Position);
					list2.Add(vertex.Normal);
					list3.Add(vertex.UVCoords);
				}
				List<int> list4 = new List<int>();
				Face[] faces = meshObject.Faces;
				for (int k = 0; k < faces.Length; k++)
				{
					Face face = faces[k];
					list4.Add((int)face.VertIndexs[0]);
					list4.Add((int)face.VertIndexs[1]);
					list4.Add((int)face.VertIndexs[2]);
				}
				MeshGeometry3D geometry = new MeshGeometry3D
				{
					Normals = new Vector3DCollection(list2),
					Positions = new Point3DCollection(list),
					TextureCoordinates = new PointCollection(list3),
					TriangleIndices = new Int32Collection(list4)
				};
				Material material = HelixToolkit.Wpf.Materials.Gray;
				if (this.TexturesCount > 0u && (long)meshObject.TexIndex < (long)((ulong)this.TexturesCount))
				{
					material = GetTexture(this.Textures[meshObject.TexIndex]);
				}
				GeometryModel3D value = new GeometryModel3D
				{
					Geometry = geometry,
					Material = material,
					BackMaterial = material
				};
				model3DGroup.Children.Add(value);
			}
			return model3DGroup;
		}
        
        public async Task<Model3DGroup> GetModelAsync()
		{
			Model3DGroup model3DGroup = new Model3DGroup();
			MeshObject[] @object = this.Object;
			for (int i = 0; i < @object.Length; i++)
			{
				MeshObject meshObject = @object[i];
				List<Point3D> list = new List<Point3D>();
				List<Vector3D> list2 = new List<Vector3D>();
				List<Point> list3 = new List<Point>();
				Vertex[] vertexes = meshObject.Vertexes;
				for (int j = 0; j < vertexes.Length; j++)
				{
					Vertex vertex = vertexes[j];
					list.Add(vertex.Position);
					list2.Add(vertex.Normal);
					list3.Add(vertex.UVCoords);
				}
				List<int> list4 = new List<int>();
				Face[] faces = meshObject.Faces;
				for (int k = 0; k < faces.Length; k++)
				{
					Face face = faces[k];
					list4.Add((int)face.VertIndexs[0]);
					list4.Add((int)face.VertIndexs[1]);
					list4.Add((int)face.VertIndexs[2]);
				}
				MeshGeometry3D geometry = new MeshGeometry3D
				{
					Normals = new Vector3DCollection(list2),
					Positions = new Point3DCollection(list),
					TextureCoordinates = new PointCollection(list3),
					TriangleIndices = new Int32Collection(list4)
				};
				Material material = HelixToolkit.Wpf.Materials.Gray;
				if (this.TexturesCount > 0u && (long)meshObject.TexIndex < (long)((ulong)this.TexturesCount))
				{
					material = await GetTextureAsync(this.Textures[meshObject.TexIndex]);
				}
				GeometryModel3D value = new GeometryModel3D
				{
					Geometry = geometry,
					Material = material,
					BackMaterial = material
				};
				model3DGroup.Children.Add(value);
			}
			return model3DGroup;
		}
	}
}
