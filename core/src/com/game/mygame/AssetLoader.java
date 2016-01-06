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
import com.badlogic.gdx.scenes.scene2d.ui.Skin;
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

	public Model loadKeyTileModel() {
		Model model = modelLoader.loadModel(Gdx.files.internal("KeyTile.g3db"));
		model.materials.first().set(ColorAttribute.createDiffuse(Color.WHITE));
		model.materials.first().set(new BlendingAttribute(true, 1.0f));
		return model;
	}

	public Model loadExitTileModel() {
		Model model = modelLoader.loadModel(Gdx.files.internal("ExitTile.g3db"));
		model.materials.first().set(new BlendingAttribute(true, 1.0f));
		model.materials.first().set(ColorAttribute.createDiffuse(
				TileColor.getGdxColor(TileColor.NONE)));
		return model;
	}

	public TextureAtlas loadPlayerAnimation(TileColor color) {
		String path = "animations/";
		switch (color) {
			case RED:
				path += "player_red/player_red.atlas";
				break;
			case GREEN:
				path += "player_green/player_green.atlas";
				break;
			case BLUE:
				path += "player_blue/player_blue.atlas";
				break;
			case YELLOW:
				path += "player_yellow/player_yellow.atlas";
				break;
			default:
				path += "player_red/player_red.atlas";
		}
		return new TextureAtlas(Gdx.files.internal(path));
	}

	public TextureAtlas loadKeyTileTextures() {
		String path = "tiles/at/keytile.atlas";
		return new TextureAtlas(Gdx.files.internal(path));
	}

	public BitmapFont loadBitmapFont(String fileName) {
		String path = "fonts/" + fileName + ".fnt";
		return new BitmapFont(Gdx.files.internal(path));
	}

	public Skin loadSkin(String fileName) {
		String path = "skins/" + fileName + ".json";
		return new Skin(Gdx.files.internal(path));
	}

}

