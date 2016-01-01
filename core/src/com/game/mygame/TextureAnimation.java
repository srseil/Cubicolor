package com.game.mygame;

import com.badlogic.gdx.graphics.g2d.TextureAtlas;
import com.badlogic.gdx.graphics.g3d.attributes.TextureAttribute;
import com.badlogic.gdx.utils.Array;

public class TextureAnimation {

	TextureAttribute texture;
	private Array<TextureAtlas.AtlasRegion> regions;
	private int region;
	private float transitionTargetTime, transitionCurrentTime;
	private float duration, passed;
	private boolean inAction;

	public TextureAnimation(TextureAttribute texture, TextureAtlas atlas,
							float duration) {
		this.texture = texture;
		this.duration = duration;
		// Set animation to beginning and start it.
		regions = atlas.getRegions();
		texture.set(regions.get(0));
		region = 0;
		transitionCurrentTime = 0.0f;
		transitionTargetTime = duration/regions.size;
		passed = 0.0f;
		inAction = true;
	}

	/*
	 * Update the animation based on the time passed since last frame.
	 * If delta is negative, the animation is played backwards.
	 */
	public void update(float delta) {
		if (!inAction)
			return;

		// Update texture if target time has passed.
		transitionCurrentTime += Math.abs(delta);
		if (transitionCurrentTime >= transitionTargetTime) {
			region += Math.signum(delta);

			// Stop animation if last texture is reached.
			if (region >= regions.size || region < 0) {
				inAction = false;
				transitionCurrentTime = 0.0f;
				return;
			}

			texture.set(regions.get(region));
			transitionCurrentTime -= transitionTargetTime;
		}

		passed += delta;
	}

	/*
	 * Reset the animation from the beginning;
	 * with or without starting it right now.
	 */
	public void reset(boolean inAction) {
		region = 0;
		transitionCurrentTime = 0.0f;
		passed = 0.0f;
		texture.set(regions.get(region));
		this.inAction = inAction;
	}

	/*
	 * Reset the animation from the end;
	 * with or without starting it in reverse right now.
	 */
	public void resetReverse(boolean inAction) {
		region = regions.size - 1;
		transitionCurrentTime = 0.0f;
		passed = 0.0f;
		texture.set(regions.get(region));
		this.inAction = inAction;
	}

	/*
	 * Return the time in seconds the animation has left to finish.
	 */
	public float getTimeLeft() {
		return (duration - passed);
	}

	public boolean isInAction() {
		return inAction;
	}
}

