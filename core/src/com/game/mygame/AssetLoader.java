package com.game.mygame;

import com.badlogic.gdx.Gdx;
import com.badlogic.gdx.assets.loaders.ModelLoader;
import com.badlogic.gdx.graphics.Color;
import com.badlogic.gdx.graphics.Texture;
import com.badlogic.gdx.graphics.g2d.BitmapFont;
import com.badlogic.gdx.graphics.g2d.TextureAtlas;
import com.badlogic.gdx.graphics.g2d.TextureRegion;
import com.badlogic.gdx.graphics.g3d.Model;
import com.badlogic.gdx.graphics.g3d.attributes.BlendingAttribute;
import com.badlogic.gdx.graphics.g3d.attributes.ColorAttribute;
import com.badlogic.gdx.graphics.g3d.attributes.TextureAttribute;
import com.badlogic.gdx.graphics.g3d.loader.G3dModelLoader;
import com.badlogic.gdx.utils.UBJsonReader;

public class AssetLoader {

	private ModelLoader modelLoader;

	public AssetLoader() {
		modelLoader = new G3dModelLoader(new UBJsonReader());
	}

	public Model loadPlayerModel() {
		Model model = modelLoader.loadModel(Gdx.files.internal("Player.g3db"));
		model.materials.first().set(ColorAttribute.createDiffuse(Color.WHITE));
		model.materials.first().set(new BlendingAttribute(true, 1.0f));
		return model;
	}

	// Does a simple diffuse look different to a texture?
	// If not, then simply call loadLockTileModel(NONE) here.
	public Model loadTileModel() {
		Model model = modelLoader.loadModel(Gdx.files.internal("Tile.g3db"));
		model.materials.first().set(ColorAttribute.createDiffuse(
				TileColor.getGdxColor(TileColor.NONE)));
		model.materials.first().set(new BlendingAttribute(true, 1.0f));
		return model;
	}

	public Model loadLockTileModel(TileColor color) {
		Model model = modelLoader.loadModel(Gdx.files.internal("Tile.g3db"));
		model.materials.first().set(ColorAttribute.createDiffuse(
				TileColor.getGdxColor(color)));
		model.materials.first().set(new BlendingAttribute(true, 1.0f));
		return model;
	}

	public Model loadKeyTileModel(TileColor color) {
		Model model = modelLoader.loadModel(Gdx.files.internal("KeyTile.g3db"));
		model.materials.first().set(ColorAttribute.createDiffuse(Color.WHITE));
		model.materials.first().set(new BlendingAttribute(true, 1.0f));
		TextureAttribute textureAttribute = model.materials.first().get(
				TextureAttribute.class, TextureAttribute.Diffuse);

		String path;
		switch (color) {
			case RED:
				path = "keytile_red.png";
			case GREEN:
				path = "keytile_green.png";
			case BLUE:
				path = "keytile_blue.png";
			case YELLOW:
				path = "keytile_yellow.png";
			default:
				path = "keytile_red.png";
		}

		Texture texture = new Texture(Gdx.files.internal("keytile_red.png"));
		textureAttribute.set(new TextureRegion(texture));
		return model;
	}

	public Model loadExitTileModel() {
		Model model = modelLoader.loadModel(Gdx.files.internal("ExitTile.g3db"));
		model.materials.first().set(new BlendingAttribute(true, 1.0f));
		model.materials.first().set(ColorAttribute.createDiffuse(Color.GOLD));
		return model;
	}

	// Implement for different colors.
	public TextureAtlas loadPlayerAnimation(TileColor color) {
		return new TextureAtlas(Gdx.files.internal("player_animation/player_animation.atlas"));
	}

	public BitmapFont loadBitmapFont(String fileName) {
		return new BitmapFont(Gdx.files.internal("fonts/" + fileName + ".fnt"));
	}

}

