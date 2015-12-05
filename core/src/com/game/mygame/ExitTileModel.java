package com.game.mygame;

import com.badlogic.gdx.graphics.g3d.Model;
import com.badlogic.gdx.graphics.g3d.ModelInstance;
import com.badlogic.gdx.graphics.g3d.utils.AnimationController;

import java.util.ArrayList;

public class ExitTileModel extends ModelInstance
		implements AnimatedModel, AnimationController.AnimationListener {

	private enum State {
		STILL,
		MOVING_DOWN,
		MOVING_UP,
		SNAPPING
	}

	private ExitTile data;
	private State state;
	private AnimationController moveAnimation;

	private ArrayList<ModelInstance> requirementModels;
	private float x, z;
	private int height;

	public ExitTileModel(Model model, ExitTile data, float x, float z) {
		super(model);
		this.data = data;
		this.x = x;
		this.z = z;
		moveAnimation = new AnimationController(this);
		moveAnimation.allowSameAnimation = true;
		moveAnimation.setAnimation("Cube|Spiral", 1, 1.0f, this);
		state = State.STILL;
		height = data.getHeight();

		requirementModels = new ArrayList<>();
		requirementModels.add(null);
		requirementModels.add(null);

		updateTransform(false);
		System.out.println(data.getHeight());
	}

	private void updateTransform(boolean snapped) {
		System.out.println(height);
		if (snapped)
			transform.setTranslation(x, 0.0f, z);
		else
			transform.setTranslation(x, (height+1) * 5.0f, z);
	}

	@Override
	public void update(float delta) {
		switch (state) {
			case MOVING_DOWN:
				moveAnimation.update(delta);
				break;
			case MOVING_UP:
				moveAnimation.update(delta);
				break;
			case SNAPPING:
				moveAnimation.update(delta);
		}

	}

	@Override
	public void updateState() {
		//requirement met during removing model?
		if (state == State.STILL &&
				data.getRequirements().size() < requirementModels.size()) {
			moveAnimation.setAnimation("Cube|Spiral", 1, 1.0f, this);
			state = State.MOVING_DOWN;
		}
		height = data.getHeight();
	}

	@Override
	public void reset() {
		// ganz ende, während snapping
		height = data.getHeight();
		System.out.println(requirementModels.size());
		if (data.getRequirements().size() == requirementModels.size()) {
			if (state == State.MOVING_DOWN) {
				// Das oder unten >=:
				requirementModels.remove(0); // Später unnötig

				state = State.MOVING_UP;
				moveAnimation.current.speed *= -1;
			}
		} else {
			System.out.println(state);
			if (state == State.STILL) {
				if (requirementModels.size() == 0) {
					state = State.MOVING_UP;
					moveAnimation.setAnimation("Cube|Fall", 1, -1.0f, this);
				} else {
					updateTransform(false); // basierend auf differenz
					moveAnimation.setAnimation("Cube|Spiral", 1, -1.0f, this);
					state = State.MOVING_UP;
				}
			} else if (state == State.MOVING_DOWN) {
				requirementModels.remove(0); // Später unnötig
				state = State.MOVING_UP;
				moveAnimation.current.speed *= -1;
			} else if (state == State.SNAPPING) {
				state = State.MOVING_UP;
				moveAnimation.current.speed *= -1;
			}
		}
	}

	@Override
	public void onEnd(AnimationController.AnimationDesc animation) {
		System.out.println("event");
		if (animation.animation.id.equals("Cube|Spiral")) {
			if (state == State.MOVING_DOWN) {
				System.out.println("h " + state);
				requirementModels.remove(0);
				if (requirementModels.size() == 0) {
					System.out.println("snap");
					state = State.SNAPPING;
					moveAnimation.setAnimation("Cube|Fall", 1, 1.0f, this);
				} else {
					System.out.println("not snap");
					state = State.STILL;
					moveAnimation.setAnimation("Cube|Spiral", 1, 1.0f, this);
				}
				updateTransform(false);
				System.out.println("h " + state);
			} else if (state == State.MOVING_UP) {
				System.out.println("reset");
				requirementModels.add(null);
				if (requirementModels.size() == data.getRequirements().size()) {
					System.out.println("reset in");
					state = State.STILL;
					moveAnimation.setAnimation("Cube|Spiral", 1, 1.0f, this);
					//moveAnimation.setAnimation("Cube|Spiral", 1, 1.0f, this);
				} else {
					moveAnimation.setAnimation("Cube|Spiral", 1, -1.0f, this);
				}
				updateTransform(false);
			}
		} else if (animation.animation.id.equals("Cube|Fall")) {
			if (state == State.MOVING_UP) {
				moveAnimation.setAnimation("Cube|Spiral", 1, -1.0f, this);

			} else {
				state = State.STILL;
				moveAnimation.setAnimation("Cube|Fall", 1, -1.0f, this);
			}
			System.out.println(height);
			//updateTransform(true);
		}
	}

	@Override
	public void onLoop(AnimationController.AnimationDesc animation) {}

}
