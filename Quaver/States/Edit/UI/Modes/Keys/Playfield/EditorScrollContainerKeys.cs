﻿using System;
using System.Collections.Generic;
using Quaver.Graphics;
using Quaver.Graphics.Base;

namespace Quaver.States.Edit.UI.Modes.Keys.Playfield
{
    internal class EditorScrollContainerKeys : Container
    {
        /// <summary>
        ///     The playfield
        /// </summary>
        internal EditorPlayfieldKeys Playfield { get; }

        /// <summary>
        ///     All of the HitObjects in the map.
        /// </summary>
        private List<EditorHitObjectKeys> HitObjects { get; set; }

        /// <inheritdoc />
        /// <summary>
        ///     Ctor - 
        /// </summary>
        /// <param name="playfield"></param>
        internal EditorScrollContainerKeys(EditorPlayfieldKeys playfield)
        {
            Playfield = playfield;

            Parent = Playfield.Container;
            Size = new UDim2D(Playfield.Width, Playfield.BackgroundContainer.SizeY);
            Alignment = Alignment.TopCenter;
            
            InitializeHitObjects();
        }

        /// <summary>
        ///     Initializes all of the HitObjects for the map.
        /// </summary>
        private void InitializeHitObjects()
        {
            HitObjects = new List<EditorHitObjectKeys>();          
            Playfield.Screen.Map.HitObjects.ForEach(x => HitObjects.Add(new EditorHitObjectKeys(this, x)));
        }

        /// <inheritdoc />
        /// <summary>
        ///     Update all of the HitObject's positions based on the song time.
        /// </summary>
        /// <param name="dt"></param>
        internal override void Update(double dt)
        {
            for (var i = 0; i < HitObjects.Count && i < 255; i++)
            {
                var hitObject = HitObjects[i];
                
                // Set new HitObject positions.
                hitObject.PositionY = hitObject.GetPosFromOffset(hitObject.OffsetYFromReceptor);
                hitObject.UpdateSpritePositions();
                //Console.WriteLine(hitObject.OffsetYFromReceptor + " " + hitObject.HitObjectSprite.PosX + " " + hitObject.PositionY +  " " + hitObject.HitObjectSprite.SizeX +  " "  + hitObject.HitObjectSprite.SizeY);
            }
            
            base.Update(dt);
        }
    }
}