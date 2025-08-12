using UnityEngine;
using UnityEngine.Tilemaps;

namespace Dungonator.Client.Layers.Maze
{
    public class ClientMazePhysicsSetup
    {
        public static void ConfigureClientColliders(Tilemap tilemap)
        {
            if (tilemap == null)
                return;

            var collider = tilemap.GetComponent<TilemapCollider2D>();
            if (collider == null)
                collider = tilemap.gameObject.AddComponent<TilemapCollider2D>();
            collider.compositeOperation = Collider2D.CompositeOperation.Merge;

            var rb = tilemap.GetComponent<Rigidbody2D>();
            if (rb == null)
                rb = tilemap.gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;

            if (tilemap.GetComponent<CompositeCollider2D>() == null)
                tilemap.gameObject.AddComponent<CompositeCollider2D>();
        }
    }
}