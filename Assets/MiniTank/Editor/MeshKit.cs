using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MiniTank.EditorTools
{
    /// <summary>
    /// Birçok basit parçayı (kutu, silindir, özel şekil) tek bir mesh'te birleştirir.
    /// Her materyal ayrı bir alt-mesh olur. Böylece detaylı bir obje sahnede tek obje olarak
    /// kalır ve mobilde performans korunur.
    /// </summary>
    public class MeshKit
    {
        readonly Dictionary<Material, List<CombineInstance>> groups = new Dictionary<Material, List<CombineInstance>>();
        readonly List<Material> order = new List<Material>();

        static Mesh cube, cylinder, sphere, capsule;
        public static Mesh Cube => cube != null ? cube : (cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx"));
        public static Mesh CylinderMesh => cylinder != null ? cylinder : (cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx"));
        public static Mesh SphereMesh => sphere != null ? sphere : (sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx"));
        public static Mesh CapsuleMesh => capsule != null ? capsule : (capsule = Resources.GetBuiltinResource<Mesh>("Capsule.fbx"));

        public bool IsEmpty => order.Count == 0;

        public void Add(Mesh mesh, Material mat, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            if (!groups.TryGetValue(mat, out var list))
            {
                list = new List<CombineInstance>();
                groups[mat] = list;
                order.Add(mat);
            }
            list.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(pos, rot, scale) });
        }

        public void Add(Mesh mesh, Material mat, Vector3 pos) => Add(mesh, mat, pos, Quaternion.identity, Vector3.one);

        public void Box(Material m, Vector3 center, Vector3 size) => Add(Cube, m, center, Quaternion.identity, size);
        public void Box(Material m, Vector3 center, Vector3 size, Quaternion rot) => Add(Cube, m, center, rot, size);

        /// <summary>Dikey silindir (Y ekseni boyunca).</summary>
        public void CylinderY(Material m, Vector3 center, float radius, float length) =>
            Add(CylinderMesh, m, center, Quaternion.identity, new Vector3(radius * 2f, length / 2f, radius * 2f));

        /// <summary>Yatay silindir (X ekseni boyunca) – tekerlek, aks.</summary>
        public void CylinderX(Material m, Vector3 center, float radius, float length) =>
            Add(CylinderMesh, m, center, Quaternion.Euler(0f, 0f, 90f), new Vector3(radius * 2f, length / 2f, radius * 2f));

        /// <summary>Yatay silindir (Z ekseni boyunca) – namlu, boru.</summary>
        public void CylinderZ(Material m, Vector3 center, float radius, float length) =>
            Add(CylinderMesh, m, center, Quaternion.Euler(90f, 0f, 0f), new Vector3(radius * 2f, length / 2f, radius * 2f));

        public void Cylinder(Material m, Vector3 center, float radius, float length, Quaternion rot) =>
            Add(CylinderMesh, m, center, rot, new Vector3(radius * 2f, length / 2f, radius * 2f));

        public void Sphere(Material m, Vector3 center, Vector3 size) => Add(SphereMesh, m, center, Quaternion.identity, size);

        public void Capsule(Material m, Vector3 center, Vector3 scale, Quaternion rot) => Add(CapsuleMesh, m, center, rot, scale);

        /// <summary>Parçaları birleştirip mesh'i proje içine kaydeder.</summary>
        public Mesh BuildMesh(string assetName, out Material[] materials)
        {
            var parts = new List<CombineInstance>();
            var temps = new List<Mesh>();
            int totalVerts = 0;

            foreach (var mat in order)
            {
                var part = new Mesh();
                var list = groups[mat];
                foreach (var ci in list) totalVerts += ci.mesh.vertexCount;
                if (totalVerts > 60000) part.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                part.CombineMeshes(list.ToArray(), true, true);
                parts.Add(new CombineInstance { mesh = part, transform = Matrix4x4.identity });
                temps.Add(part);
            }

            var final = new Mesh();
            if (totalVerts > 60000) final.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            final.CombineMeshes(parts.ToArray(), false, false);
            final.RecalculateBounds();
            final.RecalculateTangents();

            foreach (var t in temps) Object.DestroyImmediate(t);

            materials = order.ToArray();
            return SaveMesh(final, assetName);
        }

        /// <summary>Mesh'i oluşturur ve sahneye tek bir obje olarak koyar.</summary>
        public GameObject Build(string assetName, string objectName, Transform parent, Vector3 position, Quaternion rotation)
        {
            var mesh = BuildMesh(assetName, out var mats);
            return Spawn(mesh, mats, objectName, parent, position, rotation);
        }

        public static GameObject Spawn(Mesh mesh, Material[] mats, string objectName, Transform parent, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject(objectName);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            return go;
        }

        // ------------------------------------------------------------------ Kayıt

        public const string MeshFolder = "Assets/MiniTank/Generated/Meshes";

        public static Mesh SaveMesh(Mesh mesh, string assetName)
        {
            EnsureFolder(MeshFolder);
            string path = $"{MeshFolder}/{assetName}.asset";
            mesh.name = assetName;

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                // Aynı dosyayı güncelle ki prefab/sahne bağlantıları kopmasın
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }

    /// <summary>
    /// Prosedürel köşeli (low-poly) şekiller. Tüm yüzler dışa bakacak şekilde otomatik yönlendirilir.
    /// </summary>
    public static class Shapes
    {
        /// <summary>Yüz listesinden düz gölgelendirilmiş mesh. Yüzler merkeze göre dışa çevrilir.</summary>
        public static Mesh FromFaces(List<Vector3[]> faces, float uvScale = 0.5f)
        {
            Vector3 center = Vector3.zero;
            int count = 0;
            foreach (var f in faces)
                foreach (var v in f) { center += v; count++; }
            center /= Mathf.Max(1, count);

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            foreach (var face in faces)
            {
                if (face.Length < 3) continue;
                Vector3[] f = face;
                Vector3 fc = Vector3.zero;
                foreach (var v in f) fc += v;
                fc /= f.Length;

                Vector3 n = Vector3.Cross(f[1] - f[0], f[2] - f[0]);
                if (n.sqrMagnitude < 1e-10f) continue; // alanı sıfır olan yüz (bozuk normal üretir)
                if (Vector3.Dot(n, fc - center) < 0f)
                {
                    f = (Vector3[])face.Clone();
                    System.Array.Reverse(f);
                    n = -n;
                }
                n.Normalize();

                int start = verts.Count;
                foreach (var v in f)
                {
                    verts.Add(v);
                    Vector3 a = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
                    Vector2 uv = a.y >= a.x && a.y >= a.z ? new Vector2(v.x, v.z)
                               : a.x >= a.z ? new Vector2(v.z, v.y) : new Vector2(v.x, v.y);
                    uvs.Add(uv * uvScale);
                }
                for (int i = 1; i < f.Length - 1; i++)
                {
                    tris.Add(start);
                    tris.Add(start + i);
                    tris.Add(start + i + 1);
                }
            }

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>
        /// Yan kesiti (profil: x=Z ekseni, y=Y ekseni) X ekseni boyunca uzatır.
        /// Tank gövdesi, palet, çatı gibi şekiller için.
        /// </summary>
        public static Mesh Prism(Vector2[] profile, float width)
        {
            float h = width / 2f;
            var faces = new List<Vector3[]>();
            int n = profile.Length;
            var left = new Vector3[n];
            var right = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                left[i] = new Vector3(-h, profile[i].y, profile[i].x);
                right[i] = new Vector3(h, profile[i].y, profile[i].x);
            }
            faces.Add(left);
            faces.Add(right);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                faces.Add(new[] { left[i], right[i], right[j], left[j] });
            }
            return FromFaces(faces);
        }

        /// <summary>
        /// Üstten görünüşü (x, z) verilen tabanı yukarı doğru uzatır; üst yüz küçültülerek
        /// eğimli yan yüzler elde edilir. Taret, bina, bariyer için.
        /// </summary>
        public static Mesh Frustum(Vector2[] footprint, float height, float topScale, Vector2 topOffset)
        {
            int n = footprint.Length;
            var bottom = new Vector3[n];
            var top = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                bottom[i] = new Vector3(footprint[i].x, 0f, footprint[i].y);
                top[i] = new Vector3(footprint[i].x * topScale + topOffset.x, height, footprint[i].y * topScale + topOffset.y);
            }
            var faces = new List<Vector3[]> { bottom, top };
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                faces.Add(new[] { bottom[i], bottom[j], top[j], top[i] });
            }
            return FromFaces(faces);
        }

        /// <summary>Köşeli koni (çam katmanları için).</summary>
        public static Mesh Cone(float radius, float height, int segments)
        {
            var faces = new List<Vector3[]>();
            var ring = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                ring[i] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            }
            faces.Add(ring);
            Vector3 tip = new Vector3(0f, height, 0f);
            for (int i = 0; i < segments; i++)
                faces.Add(new[] { ring[i], ring[(i + 1) % segments], tip });
            return FromFaces(faces);
        }

        /// <summary>Düzensiz, köşeli kaya.</summary>
        public static Mesh Rock(int seed)
        {
            var rng = new System.Random(seed);
            int lat = 5, lon = 8;
            var pts = new Vector3[lat + 1, lon];
            for (int i = 0; i <= lat; i++)
            {
                float t = (float)i / lat;
                float phi = Mathf.Lerp(-Mathf.PI / 2f, Mathf.PI / 2f, t);
                for (int j = 0; j < lon; j++)
                {
                    float th = j * Mathf.PI * 2f / lon;
                    float r = 0.8f + (float)rng.NextDouble() * 0.4f;
                    if (i == 0 || i == lat) r = 0.9f;
                    pts[i, j] = new Vector3(Mathf.Cos(phi) * Mathf.Cos(th) * r,
                                            Mathf.Sin(phi) * r * 0.75f,
                                            Mathf.Cos(phi) * Mathf.Sin(th) * r);
                }
            }
            var faces = new List<Vector3[]>();
            for (int i = 0; i < lat; i++)
                for (int j = 0; j < lon; j++)
                {
                    int k = (j + 1) % lon;
                    Vector3 a = pts[i, j], b = pts[i, k], c = pts[i + 1, k], d = pts[i + 1, j];
                    faces.Add(new[] { a, b, c });
                    faces.Add(new[] { a, c, d });
                }
            return FromFacesStarShaped(faces);
        }

        // Kaya gibi içbükey olabilen ama merkezden bakınca "yıldız" şeklindeki objeler için:
        // yönlendirme her yüzün kendi merkezine göre yapılır (FromFaces ile aynı mantık).
        static Mesh FromFacesStarShaped(List<Vector3[]> faces) => FromFaces(faces, 0.5f);

        /// <summary>Stadyum (yuvarlak uçlu dikdörtgen) profil – palet için.</summary>
        public static Vector2[] Stadium(float length, float height, int arcSegments)
        {
            float r = height / 2f;
            float half = length / 2f - r;
            var pts = new List<Vector2>();
            for (int i = 0; i <= arcSegments; i++)
            {
                float a = -Mathf.PI / 2f + Mathf.PI * i / arcSegments; // ön yay
                pts.Add(new Vector2(half + Mathf.Cos(a) * r, r + Mathf.Sin(a) * r));
            }
            for (int i = 0; i <= arcSegments; i++)
            {
                float a = Mathf.PI / 2f + Mathf.PI * i / arcSegments; // arka yay
                pts.Add(new Vector2(-half + Mathf.Cos(a) * r, r + Mathf.Sin(a) * r));
            }
            return pts.ToArray();
        }
    }

    /// <summary>Zemin ve duvarlar için prosedürel doku üretir (PNG olarak kaydeder).</summary>
    public static class TextureFactory
    {
        public const string Folder = "Assets/MiniTank/Generated/Textures";

        public enum Kind { Sand, Snow, Asphalt, Concrete, Grain, Wear }

        public static Texture2D Create(string name, Kind kind, int seed, int size = 512)
        {
            MeshKit.EnsureFolder(Folder);
            string path = $"{Folder}/{name}.png";

            var tex = new Texture2D(size, size, TextureFormat.RGB24, true);
            var rng = new System.Random(seed);
            float ox = (float)rng.NextDouble() * 1000f, oy = (float)rng.NextDouble() * 1000f;

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size, v = (float)y / size;
                    // Dikişsiz döşeme: dört kaydırılmış örneğin konuma göre ağırlıklı ortalaması
                    float value = Sample(kind, u, v, ox, oy) * (1f - u) * (1f - v)
                                + Sample(kind, u - 1f, v, ox, oy) * u * (1f - v)
                                + Sample(kind, u, v - 1f, ox, oy) * (1f - u) * v
                                + Sample(kind, u - 1f, v - 1f, ox, oy) * u * v;
                    pixels[y * size + x] = new Color(value, value, value);
                }
            tex.SetPixels(pixels);
            tex.Apply();

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.maxTextureSize = size;
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 8;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>
        /// Aynı gürültüden normal haritası üretir: ışık yüzeydeki çizik ve lekeleri gerçekten "tutar".
        /// </summary>
        public static Texture2D CreateNormalMap(string name, Kind kind, int seed, float strength, int size = 512)
        {
            MeshKit.EnsureFolder(Folder);
            string path = $"{Folder}/{name}.png";
            var rng = new System.Random(seed);
            float ox = (float)rng.NextDouble() * 1000f, oy = (float)rng.NextDouble() * 1000f;

            var h = new float[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size, v = (float)y / size;
                    h[y * size + x] = Sample(kind, u, v, ox, oy) * (1f - u) * (1f - v)
                                    + Sample(kind, u - 1f, v, ox, oy) * u * (1f - v)
                                    + Sample(kind, u, v - 1f, ox, oy) * (1f - u) * v
                                    + Sample(kind, u - 1f, v - 1f, ox, oy) * u * v;
                }

            var tex = new Texture2D(size, size, TextureFormat.RGB24, true);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float l = h[y * size + (x - 1 + size) % size], r = h[y * size + (x + 1) % size];
                    float d = h[((y - 1 + size) % size) * size + x], t = h[((y + 1) % size) * size + x];
                    var n = new Vector3((l - r) * strength, (d - t) * strength, 1f).normalized;
                    pixels[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f);
                }
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.maxTextureSize = size;
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 4;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static float Fbm(float x, float y, int octaves)
        {
            float sum = 0f, amp = 0.5f, freq = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += (Mathf.PerlinNoise(x * freq, y * freq) - 0.5f) * amp;
                amp *= 0.5f;
                freq *= 2f;
            }
            return sum;
        }

        static float Sample(Kind kind, float u, float v, float ox, float oy)
        {
            float big = Fbm(u * 4f + ox, v * 4f + oy, 4);
            float fine = Fbm(u * 32f + ox, v * 32f + oy, 2);
            float speck = Mathf.PerlinNoise(u * 180f + ox, v * 180f + oy);
            switch (kind)
            {
                case Kind.Sand: return 0.82f + big * 0.25f + fine * 0.1f - (speck > 0.72f ? 0.08f : 0f);
                case Kind.Snow: return 0.9f + big * 0.1f + fine * 0.05f - (speck > 0.8f ? 0.04f : 0f);
                case Kind.Asphalt: return 0.72f + big * 0.2f + fine * 0.18f + (speck > 0.68f ? 0.1f : 0f) - (speck < 0.25f ? 0.08f : 0f);
                case Kind.Concrete: return 0.85f + big * 0.15f + fine * 0.1f - (speck > 0.75f ? 0.07f : 0f);
                case Kind.Wear:
                {
                    // Boyalı zırh: hafif leke, ince çizikler (açık), kir ve pas noktaları (koyu)
                    float scratch = 1f - Mathf.Abs(Mathf.PerlinNoise(u * 60f + ox, v * 6f + oy) * 2f - 1f);
                    float grime = Fbm(u * 10f + ox * 0.5f, v * 10f + oy * 0.5f, 3);
                    float value = 0.88f + big * 0.14f + fine * 0.06f;
                    if (scratch > 0.965f) value += 0.12f;
                    if (grime < -0.12f) value -= 0.12f;
                    if (speck > 0.82f) value -= 0.06f;
                    return value;
                }
                default: return 0.9f + fine * 0.12f;
            }
        }
    }
}
