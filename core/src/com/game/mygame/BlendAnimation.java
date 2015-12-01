package com.game.mygame;

import com.badlogic.gdx.graphics.g3d.Material;
import com.badlogic.gdx.graphics.g3d.ModelInstance;
import com.badlogic.gdx.graphics.g3d.attributes.BlendingAttribute;

public class BlendAnimation {

	private Material material;
	private BlendingAttribute attribute;
	private float transitionCurrentTime, transitionTargetTime;
	private boolean inAction;

	public BlendAnimation(ModelInstance model, float duration) {
		material = model.materials.first();
		attribute = (BlendingAttribute) material.get(BlendingAttribute.Type);
		transitionCurrentTime = 0;
		transitionTargetTime = duration/20.0f;
		inAction = true;
	}

	public void update(float delta) {
		if (inAction && attribute.opacity > 0.0f) {
			transitionCurrentTime += delta;
			if (transitionCurrentTime >= transitionTargetTime) {
				//System.out.println(transitionCurrentTime);
				attribute.opacity -= 0.05f;
				transitionCurrentTime -= transitionTargetTime;
			}
		} else {
			inAction = false;
		}
	}

	public void reset() {
		attribute.opacity = 1.0f;
		transitionCurrentTime = 0;
		inAction = true;
	}

	public boolean inAction() {
		return inAction;
	}

}
