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
	private float duration, passed;
	private boolean inAction;

	//private ModelInstance model;


	public TextureAnimation(TextureAttribute texture, TextureAtlas atlas, float duration) {
		//this.model = model;
		/*
		texture = model.materials.first().get(
				TextureAttribute.class, TextureAttribute.Diffuse);
				*/
		this.texture = texture;
		//System.out.println(texture.toString());
		//atlas = new TextureAtlas(Gdx.files.internal(path));
		this.atlas = atlas;
		this.duration = duration;
		regions = atlas.getRegions();
		texture.set(regions.get(0));
		//System.out.println("REGIONS: " + regions.size);
		region = 0;
		//System.out.println("REGION: " + region + " _ " + regions.get(region).toString());
		transitionCurrentTime = 0.0f;
		transitionTargetTime = duration/regions.size;
		passed = 0.0f;
		inAction = true;
	}

	public void update(float delta) {
		if (!inAction)
			return;

		transitionCurrentTime += Math.abs(delta);
		if (transitionCurrentTime >= transitionTargetTime) {
			//region = (region+1) % regions.size;
			region += Math.signum(delta);
			//System.out.println("ANIMATION REGION: " + region + ", delta: " + delta);
			if (region >= regions.size || region < 0) {
				inAction = false;
				transitionCurrentTime = 0.0f;
				return;
			}
			//model.materials.first().get(TextureAttribute.class, TextureAttribute.Diffuse).set()
			//System.out.println("REGION: " + region + " _ " + regions.get(region).toString());
			//texture = model.materials.first().get(
			//		TextureAttribute.class, TextureAttribute.Diffuse);
			texture.set(regions.get(region));
			transitionCurrentTime -= transitionTargetTime;
		}

		passed += delta;
	}

	public void reset(boolean inAction) {
		region = 0;
		transitionCurrentTime = 0.0f;
		passed = 0.0f;
		texture.set(regions.get(region));
		this.inAction = inAction;
	}

	public void resetReverse(boolean inAction) {
		region = regions.size - 1;
		transitionCurrentTime = 0.0f;
		passed = 0.0f;
		texture.set(regions.get(region));
		this.inAction = inAction;
	}

	public float getTimeLeft() {
		return (duration - passed);
	}

	public boolean isInAction() {
		return inAction;
	}
}
