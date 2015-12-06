package com.game.mygame;

import com.badlogic.gdx.graphics.g3d.Model;
import com.badlogic.gdx.graphics.g3d.ModelInstance;
import com.badlogic.gdx.graphics.g3d.utils.AnimationController;
import com.badlogic.gdx.math.Vector3;

public class PlayerModel extends ModelInstance
		implements AnimatedModel, AnimationController.AnimationListener {

	private class Move {
		int dx, dy;
		boolean moved;
		public Move(int dx, int dy, boolean moved) {
			this.dx = dx;
			this.dy = dy;
			this.moved = moved;
		}
	}

	private enum State {
		STILL,
		MOVING,
		INDICATING,
		RESETTING_UP,
		RESETTING_DOWN
	}

	private Player data;
	private State state;
	private BlendAnimation blendAnimation;
	private AnimationController moveAnimation;
	private ExitTileModel exitModel;
	private float baseX, baseY;
	private int dataX, dataY;
	private Move queuedMove;
	private boolean controllable;

	public PlayerModel(Model model, Player data, float baseX, float baseY,
					   ExitTileModel exitModel) {
		super(model);
		this.data = data;
		this.baseX = baseX;
		this.baseY = baseY;
		this.exitModel = exitModel;
		state = State.STILL;
		moveAnimation = new AnimationController(this);
		moveAnimation.allowSameAnimation = true;
		moveAnimation.setAnimation("Cube|Fall");
		blendAnimation = new BlendAnimation(this,
				moveAnimation.current.duration);
		dataX = (int) data.getX();
		dataY = (int) data.getY();
		controllable = true;
		updateTransform(0, 0);
	}

	private void updateTransform(float correctX, float correctY) {
		transform.setTranslation(
				baseX + (data.getX() + correctX) * 10.0f, 7.5f,
				baseY - (data.getY() + correctY) * 10.0f);
	}

	private void triggerMovement(int dx, int dy, boolean moved) {
		controllable = false;
		dataX = (int) data.getX();
		dataY = (int) data.getY();

		if (dataX == exitModel.getColumn() &&
				dataY == exitModel.getRow() &&
				!exitModel.isTraversable()) {
			return;
		}

		if (dx == 0 && dy == 1) {
			transform.setToRotation(0, 1, 0, 0);
			if (moved) updateTransform(0, -1);
		} else if (dx == 0 && dy == -1) {
			transform.setToRotation(0, 1, 0, 180);
			if (moved) updateTransform(0, 1);
		} else if (dx == 1 && dy == 0) {
			transform.setToRotation(0, 1, 0, 270);
			if (moved) updateTransform(-1, 0);
		} else if (dx == -1 && dy == 0) {
			transform.setToRotation(0, 1, 0, 90);
			if (moved) updateTransform(1, 0);
		}

		if (moved) {
			moveAnimation.setAnimation("Cube|Movement", 1, 3.0f, this);
			state = State.MOVING;
		} else {
			updateTransform(0, 0);
			moveAnimation.setAnimation("Cube|Indication", 1, 2.5f, this);
			state = State.INDICATING;
		}
	}

	private void triggerQueuedMove() {
		if (queuedMove != null) {
			triggerMovement(queuedMove.dx, queuedMove.dy, queuedMove.moved);
			queuedMove = null;
		}
	}

	@Override
	public void update(float delta) {
		switch (state) {
			case MOVING: case INDICATING:
				moveAnimation.update(delta);
				//System.out.println(moveAnimation.current.duration -
				//		moveAnimation.current.time + " ");
				if ((moveAnimation.current.duration -
						moveAnimation.current.time) < 0.2f) {
					controllable = true;
					//System.out.println("controllable");
				}
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
		System.out.println("notified ");
		/*
			int dx = (int) data.getX() - dataX;
			int dy = (int) data.getY() - dataY;
			boolean moved;
			System.out.println(dx + " " + dy);

			if (dx != 0 || dy != 0)
				moved = true;
			else
				moved = false;

			if (state == State.MOVING) {
				queuedMove = new Move(dx, dy, moved);
			} else {
				triggerMovement(dx, dy, moved);
				dataX = (int) data.getX();
				dataY = (int) data.getY();
			}
			*/
	}

	@Override
	public void reset() {
		queuedMove = null;
		controllable = false;

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
			triggerQueuedMove();
		} else if (animation.animation.id.equals("Cube|Indication")) {
			state = State.STILL;
			moveAnimation.setAnimation("Cube|Movement");
			triggerQueuedMove();
		} else if (animation.animation.id.equals("Cube|Fall")) {
			if (state == State.RESETTING_UP) {
				System.out.println("up done");
				state = State.RESETTING_DOWN;
				//playerModel.transform.setToRotation(0, 1, 0, 0);
				updateTransform(0, 0.5f);
				moveAnimation.setAnimation("Cube|Fall", 1, -1.0f, this);
				blendAnimation.reset(0.0f);
			} else if (state == State.RESETTING_DOWN) {
				state = State.STILL;
				controllable = true;
				//moveAnimation.setAnimation("Cube|Movement");
			}
		}
	}

	@Override
	public void onLoop(AnimationController.AnimationDesc animation) {}

	public void move(int dx, int dy) {
		boolean moved;
		if (data.getX() != dataX || data.getY() != dataY)
			moved = true;
		else
			moved = false;

		if (state == State.MOVING)
			queuedMove = new Move(dx, dy, moved);
		else
			triggerMovement(dx, dy, moved);
	}

	public boolean isControllable() {
		return controllable;
	}

	public boolean hasCompleted() {
		System.out.println("called completed");
		if (state == State.STILL && data.getX() == exitModel.getColumn() &&
				data.getY() == exitModel.getRow()) {
			System.out.println("called completed -> true");
			return true;
		} else {
			return false;
		}
	}

}
