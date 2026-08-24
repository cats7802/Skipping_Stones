using UnityEngine;

public static class PebbleMeshGenerator
{
    /// <summary>
    /// 매끄럽고 납작한 조약돌(물수제비용 돌) 3D 메시를 절차적으로 생성합니다. (기존 대비 1/5 아담한 크기)
    /// </summary>
    public static Mesh CreatePebbleMesh(float width = 0.12f, float length = 0.16f, float thickness = 0.035f, int segments = 24)
    {
        Mesh mesh = new Mesh { name = "SkippingPebbleMesh" };

        int vertexCount = (segments + 1) * 3;
        Vector3[] vertices = new Vector3[vertexCount];
        Vector3[] normals = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];

        // 윗면 중앙점 (0), 아랫면 중앙점 (1)
        vertices[0] = new Vector3(0, thickness * 0.5f, 0);
        normals[0] = Vector3.up;
        uvs[0] = new Vector2(0.5f, 0.5f);

        vertices[1] = new Vector3(0, -thickness * 0.5f, 0);
        normals[1] = Vector3.down;
        uvs[1] = new Vector2(0.5f, 0.5f);

        int rimStart = 2;
        int rimCount = segments;

        // 가장자리 링 버텍스 생성 (약간의 유선형 타원형)
        for (int i = 0; i < segments; i++)
        {
            float rad = (float)i / segments * Mathf.PI * 2f;
            // 타원 형태 + 살짝 납작한 조약돌 곡선
            float x = Mathf.Sin(rad) * (width * 0.5f);
            float z = Mathf.Cos(rad) * (length * 0.5f);

            // 상단 림
            int topIdx = rimStart + i;
            vertices[topIdx] = new Vector3(x * 0.95f, thickness * 0.25f, z * 0.95f);
            normals[topIdx] = (new Vector3(x, thickness, z)).normalized;
            uvs[topIdx] = new Vector2(0.5f + Mathf.Sin(rad) * 0.45f, 0.5f + Mathf.Cos(rad) * 0.45f);

            // 외곽 림
            int midIdx = rimStart + segments + i;
            vertices[midIdx] = new Vector3(x, 0, z);
            normals[midIdx] = (new Vector3(x, 0, z)).normalized;
            uvs[midIdx] = new Vector2(0.5f + Mathf.Sin(rad) * 0.5f, 0.5f + Mathf.Cos(rad) * 0.5f);

            // 하단 림
            int botIdx = rimStart + segments * 2 + i;
            vertices[botIdx] = new Vector3(x * 0.95f, -thickness * 0.25f, z * 0.95f);
            normals[botIdx] = (new Vector3(x, -thickness, z)).normalized;
            uvs[botIdx] = new Vector2(0.5f + Mathf.Sin(rad) * 0.45f, 0.5f + Mathf.Cos(rad) * 0.45f);
        }

        // 트라이앵글 인덱스 연결
        System.Collections.Generic.List<int> triangles = new System.Collections.Generic.List<int>();

        // 윗면 팬
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            triangles.Add(0);
            triangles.Add(rimStart + next);
            triangles.Add(rimStart + i);
        }

        // 상단 -> 외곽 연결
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            int t1 = rimStart + i;
            int t2 = rimStart + next;
            int m1 = rimStart + segments + i;
            int m2 = rimStart + segments + next;

            triangles.Add(t1);
            triangles.Add(t2);
            triangles.Add(m1);

            triangles.Add(m1);
            triangles.Add(t2);
            triangles.Add(m2);
        }

        // 외곽 -> 하단 연결
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            int m1 = rimStart + segments + i;
            int m2 = rimStart + segments + next;
            int b1 = rimStart + segments * 2 + i;
            int b2 = rimStart + segments * 2 + next;

            triangles.Add(m1);
            triangles.Add(m2);
            triangles.Add(b1);

            triangles.Add(b1);
            triangles.Add(m2);
            triangles.Add(b2);
        }

        // 아랫면 팬
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            triangles.Add(1);
            triangles.Add(rimStart + segments * 2 + i);
            triangles.Add(rimStart + segments * 2 + next);
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();

        return mesh;
    }
}
