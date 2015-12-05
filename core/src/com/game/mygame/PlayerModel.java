package com.game.mygame;

import com.badlogic.gdx.graphics.g3d.Model;
import com.badlogic.gdx.graphics.g3d.ModelInstance;
import com.badlogic.gdx.graphics.g3d.utils.AnimationController;
import com.badlogic.gdx.math.Vector3;

public class PlayerModel extends ModelInstance
		implements AnimatedModel, AnimationController.AnimationListener {


	private enum State {
		STILL,
		MOVING,
		RESETTING_UP,
		RESETTING_DOWN
	}

	private Player data;
	private State state;
	private BlendAnimation blendAnimation;
	private AnimationController moveAnimation;
	private float baseX, baseY;
	private int oldDataX, oldDataY;

	public PlayerModel(Model model, Player data, float baseX, float baseY) {
		super(model);
		this.data = data;
		this.baseX = baseX;
		this.baseY = baseY;
		state = State.STILL;
		moveAnimation = new AnimationController(this);
		moveAnimation.allowSameAnimation = true;
		moveAnimation.setAnimation("Cube|Fall");
		blendAnimation = new BlendAnimation(this,
				moveAnimation.current.duration);
		oldDataX = (int) data.getX();
		oldDataY = (int) data.getY();
		updateTransform(0, 0);
	}

	private void updateTransform(float correctX, float correctY) {
		transform.setTranslation(
				baseX + (data.getX() + correctX) * 10.0f, 7.5f,
				baseY - (data.getY() + correctY) * 10.0f);
	}

	@Override
	public void update(float delta) {
		switch (state) {
			case MOVING:
				moveAnimation.update(delta);
				break;
			case RESETTING_UP:
				blendAnimation.update(delta);
				moveAnimation.update(delta);
				break;
			case RESETTING_DOWN:
				blendAnimation.update(-delta);
				moveAnimation.update(delta);
		}
	}

	@Override
	public void updateState() {
		if (data.getX() != oldDataX || data.getY() != oldDataY) {
			moveAnimation.setAnimation("Cube|Movement", 1, 3.0f, this);
			state = state.MOVING;
			oldDataX = (int) data.getX();
			oldDataY = (int) data.getY();
		}
	}

	@Override
	public void reset() {
		Vector3 corrected = new Vector3();
		transform.getTranslation(corrected);
		corrected.z -= 5f;

		// Change material to standard
		transform.setToRotation(0, 1, 0, 0);
		transform.setToTranslation(corrected);

		moveAnimation.setAnimation("Cube|Fall", 1, 1.0f, this);
		state = State.RESETTING_UP;
		//oldPlayerKey = TileAttributes.TColor.NONE;
	}

	@Override
	public void onEnd(AnimationController.AnimationDesc animation) {
		if (animation.animation.id.equals("Cube|Movement")) {
			state = State.STILL;
			moveAnimation.setAnimation("Cube|Movement");
			transform.setToRotation(0, 1, 0, 0);
			updateTransform(0, 0);
		} else if (animation.animation.id.equals("Cube|Fall")) {
			if (state == State.RESETTING_UP) {
				System.out.println("up done");
				state = State.RESETTING_DOWN;
				//playerModel.transform.setToRotation(0, 1, 0, 0);
				updateTransform(0, 0.5f);
				moveAnimation.setAnimation("Cube|Fall", 1, -1.0f, this);
				blendAnimation.reset(0.0f);
			} else if (state == State.RESETTING_DOWN){
				state = State.STILL;
				//moveAnimation.setAnimation("Cube|Movement");
			}

		}
	}

	@Override
	public void onLoop(AnimationController.AnimationDesc animation) {}

	public void move(int x, int y, boolean moved) {
		if (x == 0 && y == 1) {
			transform.setToRotation(0, 1, 0, 0);
			if (moved) updateTransform(0, -1);
		} else if (x == 0 && y == -1) {
			transform.setToRotation(0, 1, 0, 180);
			if (moved) updateTransform(0, 1);
		} else if (x == 1 && y == 0) {
			transform.setToRotation(0, 1, 0, 270);
			if (moved) updateTransform(-1, 0);
		} else if (x == -1 && y == 0) {
			transform.setToRotation(0, 1, 0, 90);
			if (moved) updateTransform(1, 0);
		}

		if (!moved)
			updateTransform(0, 0);

		moveAnimation.setAnimation("Cube|Movement", 1, 3.0f, this);
		state = State.MOVING;
	}

	public boolean isMoving() {
		if (state == State.MOVING)
			return true;
		else
			return false;
	}

}
