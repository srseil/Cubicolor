package com.game.mygame;

import com.badlogic.gdx.graphics.g2d.TextureAtlas;
import com.badlogic.gdx.graphics.g3d.Model;
import com.badlogic.gdx.graphics.g3d.attributes.TextureAttribute;

public class KeyTileModel extends TileModel {

	private KeyTile data;
	private TextureAttribute textureAttribute;
	private TextureAtlas.AtlasRegion key, none;

	public KeyTileModel(Model model, KeyTile data, int row, int column,
						MyGame game) {
		super(model, data, row, column);
		this.data = data;
		textureAttribute = materials.first().get(
				TextureAttribute.class, TextureAttribute.Diffuse);
		key = game.getKeyTileTexture(data.getKeyColor());
		none = game.getKeyTileTexture(TileColor.NONE);
		textureAttribute.set(key);
	}

	@Override
	public void updateState(Object... args) {
		// Change texture to not show key anymore.
		if (data.isDead())
			textureAttribute.set(none);
		// Proceed with parent's updateState().
		super.updateState(args);
	}

	@Override
	public void reset() {
		// Reset texture and proceed with TileModel's reset().
		textureAttribute.set(key);
		super.reset();
	}

}

