using System;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Microsoft.Xna.Framework.Net;
using Microsoft.Xna.Framework.Storage;

namespace acpl.Assets
{
    public enum CommandType { SET, CHANGE, MOVE, ADD, RESET }
    public enum AssetType { ACTOR, CONDITION, KEY, LOCATION }

    public class Asset
    {
        public AssetType type;
        public String name;

        public Asset()
        {
        }

        public Asset(String name, GraphicsDevice graphicsDevice = null)
        {
            this.name = name;
        }

        public virtual Texture2D getSubAsset(String subAsset)
        {
            return null;
        }
    }

    public class GraphicalAsset : Asset
    {
        protected AssetGraphicStore textures;

        public GraphicalAsset() { }

        public GraphicalAsset(String name, GraphicsDevice graphicsDevice)
        {
            this.name = name;
            textures = new AssetGraphicStore("images", name, graphicsDevice);
        }

        public override Texture2D getSubAsset(String subAsset)
        {
            AssetGraphic ag = textures.getSubAsset(subAsset);
            if (ag != null)
                return ag.texture;
            else
                return null;
        }
    }

    public class ActorAsset : GraphicalAsset
    {
        public AssetType type = AssetType.ACTOR;

        public ActorAsset(String name, GraphicsDevice graphicsDevice)
        {
            this.name = name;
            textures = new AssetGraphicStore("images\\actors", name, graphicsDevice);
        }
    }

    public class LocationAsset : GraphicalAsset
    {
        public AssetType type = AssetType.LOCATION;

        public LocationAsset(String name, GraphicsDevice graphicsDevice)
        {
            this.name = name;
            textures = new AssetGraphicStore("images\\backgrounds", name, graphicsDevice);
        }
    }

    public class AssetGraphic
    {
        public Texture2D texture;
        public String subAsset;

        public AssetGraphic(String fileName, String subAsset, GraphicsDevice graphicsDevice)
        {
            Debug.WriteLine("LOADING " + fileName);
            this.subAsset = subAsset;
            using (System.IO.FileStream stream = System.IO.File.OpenRead(fileName))
            {
                texture = Texture2D.FromStream(graphicsDevice, stream);
            }
        }
    }

    public class AssetGraphicStore
    {
        public List<AssetGraphic> textures = new List<AssetGraphic>();
        private String nameRegex = "";

        public AssetGraphicStore(String directory, String rootName, GraphicsDevice graphicsDevice)
        {
            this.nameRegex = rootName + "_([a-zA-Z]+)";

            String[] paths;
            try
            {
                paths = Directory.GetFiles(@directory);
                foreach (String path in paths)
                {
                    //Debug.WriteLine("Testing " + path);
                    Match match = Regex.Match(path, nameRegex);
                    if (match.Groups.Count > 1)
                    {
                        Debug.WriteLine("Loading: ");
                        Debug.WriteLine("\tAsset: " + rootName);
                        Debug.WriteLine("\tSubAsset: " + match.Groups[1].Value);
                        Debug.WriteLine("");
                        textures.Add(new AssetGraphic(path, match.Groups[1].Value, graphicsDevice));
                    }
                }
            }
            catch (Exception e)
            {
                Console.Out.WriteLine(e.Data);
            }
        }

        public AssetGraphic getSubAsset(String subAsset)
        {
            foreach (AssetGraphic texture in textures)
            {
                if (subAsset == texture.subAsset)
                    return texture;
            }

            return null;
        }
    }

    public class AssetStore
    {
        private List<Asset> assets = new List<Asset>();

        public void Add(Asset asset)
        {
            if (asset != null)
            {
                assets.Add(asset);
            }
        }

        public Asset Find(String name)
        {
            foreach (Asset asset in assets)
            {
                if (asset.name == name)
                    return asset;
            }

            return null;
        }
    }
}
