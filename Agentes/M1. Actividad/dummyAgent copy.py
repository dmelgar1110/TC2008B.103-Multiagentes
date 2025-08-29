debug = True
from time import time
import agentpy as ap
import json
import random
import numpy as np
import time

import socket
s=socket.socket(socket.AF_INET, socket.SOCK_STREAM)
s.connect(("127.0.0.1", 1104))
from_server = s.recv(4096)
print ("Received from server: ",from_server.decode("ascii"))


class Agent(ap.Agent):

    def setup(self):
        # Agent's position. It is regarded as its state.
        self.pos = (0, 0)
        self.movimientos = 0
        self.celdasLimpiadas = 0

    def get_position(self):
        return self.model.environment.positions[self]

    def execute(self):
        #obtener x y del agente
        x, y = self.get_position()
        print(f"Agente en posición ({x}, {y})")
        msg = {
            "type": "step",
            "x": int(x),
            "y": int(y)
        }
        s.send((json.dumps(msg) + "\n").encode("utf-8"))
        # Si la celda está sucia, limpia
        if not self.model.environment.sucias[x, y]:
            self.model.environment.sucias[x, y] = True
            self.celdasLimpiadas += 1
        else:
            # Generar dx y dy aleatorios entre -1 y 1, pero no puede ser (0,0)
            while True:
                cambioX = random.randint(-1, 1)
                cambioY = random.randint(-1, 1)
                if cambioX != 0 or cambioY != 0:
                    break
            nuevoX = x + cambioX
            nuevoY = y + cambioY
            if 0 <= nuevoX < self.model.environment.shape[0] and 0 <= nuevoY < self.model.environment.shape[1]:
                self.model.environment.move_to(self, (nuevoX, nuevoY))
                self.movimientos += 1
        time.sleep(0.1)


class Environment(ap.Grid):
    def setup(self):
        # Matriz de celdas sucias (False = sucia, True = limpia)
        self.sucias = np.full(self.shape, True)
        porcentaje_sucias = self.model.p.porcentaje_sucias
        num_sucias = int(np.prod(self.shape) * porcentaje_sucias / 100)
        contador = 0
        while contador < num_sucias:
            i = random.randint(0, self.shape[0]-1)
            j = random.randint(0, self.shape[1]-1)
            if self.sucias[i, j]:
                self.sucias[i, j] = False
                contador += 1

class Model(ap.Model):

    def setup(self):
        n, m = self.p.tablero
        nAgentes = self.p.nAgentes

        self.environment = Environment(self, (n, m))
        self.environment.setup()
        sucias_list = self.environment.sucias.tolist()
        print(sucias_list)
        msg = {
            "type": "setup",
            "sucias": sucias_list
        }
        s.send((json.dumps(msg) + "\n").encode("utf-8"))
        time.sleep(0.1)

        

        self.agentes = ap.AgentList(self, nAgentes, Agent)
        self.environment.add_agents(self.agentes, positions=[(1,1)]*nAgentes)

    def step(self):
        self.environment.agents.execute()

    def update(self):
        if self.environment.sucias.all():
            self.stop()

parameters = {
    'print': False,
    'tablero': (7, 7),
    'porcentaje_sucias': 50,
    'nAgentes': 1,
    'steps': 500
}

Model = Model(parameters)
result = Model.run()

msg = {"type": "end"}
s.send((json.dumps(msg) + "\n").encode("utf-8"))
time.sleep(0.1)
#s.send(b"$")
s.close()
