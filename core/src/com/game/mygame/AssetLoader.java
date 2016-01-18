package com.game.mygame;

import com.badlogic.gdx.Gdx;
import com.badlogic.gdx.assets.loaders.ModelLoader;
import com.badlogic.gdx.audio.Sound;
import com.badlogic.gdx.files.FileHandle;
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
import com.badlogic.gdx.scenes.scene2d.utils.TextureRegionDrawable;
import com.badlogic.gdx.utils.UBJsonReader;

import java.io.File;
import java.util.HashMap;

public class AssetLoader {

	private ModelLoader modelLoader;

	public AssetLoader() {
		modelLoader = new G3dModelLoader(new UBJsonReader());
	}

	public Model loadPlayerModel() {
		String path = "models/player.g3db";
		Model model = modelLoader.loadModel(Gdx.files.internal(path));
		model.materials.first().set(ColorAttribute.createDiffuse(Color.WHITE));
		model.materials.first().set(ColorAttribute.createSpecular(0.7f, 0.7f, 0.7f, 1.0f));
		model.materials.first().set(new BlendingAttribute(true, 1.0f));
		return model;
	}

	public Model loadTileModel() {
		return loadLockTileModel(TileColor.NONE);
	}

	public Model loadLockTileModel(TileColor color) {
		String path = "models/tile.g3db";
		Model model = modelLoader.loadModel(Gdx.files.internal(path));
		model.materials.first().set(ColorAttribute.createDiffuse(
				TileColor.getGdxColor(color)));
		model.materials.first().set(ColorAttribute.createSpecular(0.7f, 0.7f, 0.7f, 1.0f));
		model.materials.first().set(new BlendingAttribute(true, 1.0f));
		return model;
	}

	public Model loadKeyTileModel() {
		String path = "models/keytile.g3db";
		Model model = modelLoader.loadModel(Gdx.files.internal(path));
		model.materials.first().set(ColorAttribute.createDiffuse(Color.WHITE));
		model.materials.first().set(ColorAttribute.createSpecular(0.7f, 0.7f, 0.7f, 1.0f));
		model.materials.first().set(new BlendingAttribute(true, 1.0f));
		return model;
	}

	public Model loadExitTileModel() {
		String path = "models/exittile.g3db";
		Model model = modelLoader.loadModel(Gdx.files.internal(path));
		model.materials.first().set(ColorAttribute.createDiffuse(Color.WHITE));
		model.materials.first().set(ColorAttribute.createSpecular(0.7f, 0.7f, 0.7f, 1.0f));
		model.materials.first().set(new BlendingAttribute(true, 1.0f));

		path = "textures/exittile/exittile.atlas";
		TextureAttribute textureAttribute = model.materials.first().get(
				TextureAttribute.class, TextureAttribute.Diffuse);
		TextureAtlas atlas = new TextureAtlas(Gdx.files.internal(path));
		textureAttribute.set(atlas.getRegions().first());
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
		String path = "textures/keytile/keytile.atlas";
		return new TextureAtlas(Gdx.files.internal(path));
	}

	public TextureAtlas.AtlasRegion loadExitTileTexture() {
		String path = "textures/exittile/exittile.atlas";
		TextureAtlas atlas = new TextureAtlas(Gdx.files.internal(path));
		return atlas.getRegions().first();
	}

	public TextureRegionDrawable loadMenuBackground() {
		String path = "textures/menu/background.png";
		TextureRegionDrawable background = new TextureRegionDrawable(
				new TextureRegion(new Texture(Gdx.files.internal(path))));
		return background;
	}

	public Sound loadSound(String fileName) {
		String path = "sounds/" + fileName + ".wav";
		return Gdx.audio.newSound(Gdx.files.internal(path));
	}

	public BitmapFont loadBitmapFont(String fileName) {
		String path = "fonts/" + fileName + ".fnt";
		return new BitmapFont(Gdx.files.internal(path));
	}

	public HashMap<String, BitmapFont> loadBitmapFonts() {
		HashMap<String, BitmapFont> fonts = new HashMap<>();
		String path = "fonts/";
		FileHandle[] files = Gdx.files.internal("fonts/").list(".fnt");
		BitmapFont font;
		String name;
		for (FileHandle fileHandle : files) {
			font = new BitmapFont(Gdx.files.internal(fileHandle.path()));
			name = fileHandle.pathWithoutExtension().substring(path.length());
			fonts.put(name, font);
		}
		return fonts;
	}

	public Skin loadSkin(String fileName) {
		String path = "skins/" + fileName + ".json";
		return new Skin(Gdx.files.internal(path));
	}

}

