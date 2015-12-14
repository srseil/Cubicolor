package com.game.mygame;

import com.badlogic.gdx.Gdx;
import com.badlogic.gdx.graphics.g2d.TextureAtlas;
import com.badlogic.gdx.graphics.g3d.ModelInstance;
import com.badlogic.gdx.graphics.g3d.attributes.TextureAttribute;
import com.badlogic.gdx.utils.Array;

public class TextureAnimation {

	TextureAttribute texture;
	private TextureAtlas atlas;
	private Array<TextureAtlas.AtlasRegion> regions;
	private int region;
	private float transitionTargetTime, transitionCurrentTime;
	private boolean inAction;

	private ModelInstance model;

	public TextureAnimation(String path, ModelInstance model, float duration) {
		this.model = model;
		texture = model.materials.first().get(
				TextureAttribute.class, TextureAttribute.Diffuse);
		System.out.println(texture.toString());
		atlas = new TextureAtlas(Gdx.files.internal(path));
		regions = atlas.getRegions();
		texture.set(regions.get(0));
		System.out.println("REGIONS: " + regions.size);
		region = 0;
		System.out.println("REGION: " + region + " _ " + regions.get(region).toString());
		transitionCurrentTime = 0.0f;
		transitionTargetTime = duration/regions.size;
		inAction = true;
	}

	public void update(float delta) {
		if (!inAction)
			return;

		transitionCurrentTime += Math.abs(delta);
		if (transitionCurrentTime >= transitionTargetTime) {
			//region = (region+1) % regions.size;
			region += Math.signum(delta);
			System.out.println("ANIMATION REGION: " + region + ", delta: " + delta);
			if (region >= regions.size || region < 0) {
				inAction = false;
				transitionCurrentTime = 0.0f;
				return;
			}
			//model.materials.first().get(TextureAttribute.class, TextureAttribute.Diffuse).set()
			System.out.println("REGION: " + region + " _ " + regions.get(region).toString());
			//texture = model.materials.first().get(
			//		TextureAttribute.class, TextureAttribute.Diffuse);
			texture.set(regions.get(region));
			transitionCurrentTime -= transitionTargetTime;
		}
	}

	public void reset() {
		region = 0;
		transitionCurrentTime = 0.0f;
		inAction = true;
	}

	public void resetReverse() {
		transitionCurrentTime = 0.0f;
		inAction = true;
	}

	public boolean isInAction() {
		return inAction;
	}
}
