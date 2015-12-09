package com.game.mygame;

import com.badlogic.gdx.graphics.g3d.Model;
import com.badlogic.gdx.graphics.g3d.ModelInstance;
import com.badlogic.gdx.graphics.g3d.utils.AnimationController;
import com.badlogic.gdx.math.Vector3;

public class PlayerModel extends ModelInstance
		implements Observer, AnimationController.AnimationListener {

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
	private TileModel[][] modelMatrix;
	private ExitTileModel exitModel;
	private float baseX, baseY;
	private int dataX, dataY;
	private Move queuedMove;
	private boolean controllable;

	public PlayerModel(Model model, Player data, float baseX, float baseY,
					   TileModel[][] modelMatrix, ExitTileModel exitModel) {
		super(model);
		this.data = data;
		this.baseX = baseX;
		this.baseY = baseY;
		this.modelMatrix = modelMatrix;
		this.exitModel = exitModel;
		state = State.STILL;
		moveAnimation = new AnimationController(this);
		moveAnimation.allowSameAnimation = true;
		moveAnimation.setAnimation("Cube|Fall");
		blendAnimation = new BlendAnimation(this,
				moveAnimation.current.duration);
		dataX = data.getX();
		dataY = data.getY();
		controllable = true;
		updateTransform(0, 0);
	}

	private void updateTransform(float correctX, float correctY) {
		transform.setTranslation(
				0*baseX + (data.getX() + correctX) * TileModel.SIZE,
				0.75f * TileModel.SIZE,
				0*baseY - (data.getY() + correctY) * TileModel.SIZE);
	}

	private void triggerMovement(int dx, int dy, boolean moved) {
		controllable = false;
		dataX = data.getX();
		dataY = data.getY();

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

	@Override public void updateState() {
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

	public void reset() {
		queuedMove = null;
		controllable = false;

		dataX = data.getX();
		dataY = data.getY();

		Vector3 corrected = new Vector3();
		transform.getTranslation(corrected);
		corrected.z -= TileModel.SIZE/2;

		// Change material to standard
		transform.setToRotation(0, 1, 0, 0);
		transform.setToTranslation(corrected);

		moveAnimation.setAnimation("Cube|Fall", 1, 1.0f, this);
		state = State.RESETTING_UP;
		//oldPlayerKey = TileAttributes.TColor.NONE;
	}

	public void move(int dx, int dy) {
		if (data.getX() == exitModel.getColumn() &&
				data.getY() == exitModel.getRow()) {
			if (!exitModel.isTraversable()) {
				modelMatrix[dataY][dataX].hold();
				return;
			} else {
				modelMatrix[dataY][dataX].release();
			}
		}

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
		if (state == State.STILL && dataX == exitModel.getColumn() &&
				dataY == exitModel.getRow()) {
			return true;
		} else {
			return false;
		}
	}

}
