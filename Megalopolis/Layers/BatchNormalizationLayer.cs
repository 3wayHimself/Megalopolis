﻿using System;
using System.Linq;

namespace Megalopolis
{
    namespace Layers
    {
        public class BatchNormalizationLayer : Layer
        {
            private double[] gamma = null;
            private double[] beta = null;
            private double momentum = 0.9;
            private double[] means = null;
            private double[] variances = null;
            private double[] standardDeviations = null;
            private double[,] xc = null;
            private double[,] xn = null;

            public BatchNormalizationLayer(Layer layer, Func<int, int, int, double> func) : base(layer, layer.Outputs)
            {
                this.gamma = new double[layer.Outputs];
                this.beta = new double[layer.Outputs];
                this.means = new double[layer.Outputs];
                this.variances = new double[layer.Outputs];

                for (int i = 0; i < layer.Outputs; i++)
                {
                    this.gamma[i] = 1;
                    this.beta[i] = 0;
                    this.means[i] = 0;
                    this.variances[i] = 0;
                }
            }

            public BatchNormalizationLayer(Layer layer, Func<int, int, int, double> func, double momentum) : base(layer, layer.Outputs)
            {
                this.gamma = new double[layer.Outputs];
                this.beta = new double[layer.Outputs];
                this.means = new double[layer.Outputs];
                this.variances = new double[layer.Outputs];
                this.momentum = momentum;

                for (int i = 0; i < layer.Outputs; i++)
                {
                    this.gamma[i] = 1;
                    this.beta[i] = 0;
                    this.means[i] = 0;
                    this.variances[i] = 0;
                }
            }

            public override Batch<double[]> PropagateForward(Batch<double[]> inputs, bool isTraining)
            {
                var outputs = new Batch<double[]>(new double[inputs.Size][]);

                this.xc = new double[inputs.Size, inputs[0].Length];
                this.xn = new double[inputs.Size, inputs[0].Length];

                if (isTraining)
                {
                    var meanVector = new double[inputs[0].Length];
                    var varianceVector = new double[inputs[0].Length];
                    
                    this.standardDeviations = new double[inputs[0].Length];

                    for (int i = 0; i < meanVector.Length; i++)
                    {
                        meanVector[i] = 0;
                        varianceVector[i] = 0;
                    }

                    for (int i = 0; i < meanVector.Length; i++)
                    {
                        for (int j = 0; j < inputs.Size; j++)
                        {
                            meanVector[i] += inputs[j][i];
                        }
                    }

                    for (int i = 0; i < meanVector.Length; i++)
                    {
                        meanVector[i] = meanVector[i] / inputs.Size;
                        this.means[i] = this.momentum * this.means[i] + (1 - this.momentum) * meanVector[i];
                    }

                    for (int i = 0; i < meanVector.Length; i++)
                    {
                        for (int j = 0; j < inputs.Size; j++)
                        {
                            this.xc[j, i] = inputs[j][i] - meanVector[i];
                            varianceVector[i] += this.xc[j, i] * this.xc[j, i];
                        }
                    }

                    for (int i = 0; i < varianceVector.Length; i++)
                    {
                        varianceVector[i] = varianceVector[i] / inputs.Size;
                        this.standardDeviations[i] = Math.Sqrt(varianceVector[i] + 10e-7);
                        this.variances[i] = this.momentum * this.variances[i] + (1 - this.momentum) * varianceVector[i];
                    }

                    for (int i = 0; i < inputs[0].Length; i++)
                    {
                        for (int j = 0; j < inputs.Size; j++)
                        {
                            this.xn[j, i] = this.xc[j, i] / this.standardDeviations[i];
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < inputs[0].Length; i++)
                    {
                        for (int j = 0; j < inputs.Size; j++)
                        {
                            this.xc[j, i] = inputs[j][i] - this.means[i];
                            this.xn[j, i] = this.xc[j, i] / Math.Sqrt(this.variances[i] + 10e-7);
                        }
                    }
                }

                for (int i = 0; i < inputs.Size; i++)
                {
                    outputs[i] = new double[this.outputs];

                    for (int j = 0; j < this.outputs; j++)
                    {
                        outputs[i][j] = this.gamma[j] * this.xc[i, j] + this.beta[j];
                    }
                }

                return outputs;
            }

            public override Tuple<Batch<double[]>, Batch<double[]>> PropagateBackward(Batch<double[]> inputs, Batch<double[]> outputs, Batch<double[]> deltas)
            {
                var dbetaVector = new double[deltas[0].Length];
                var dgammaVector = new double[deltas[0].Length];
                var dxn = new double[deltas.Size, deltas[0].Length];
                var dxc = new double[deltas.Size, deltas[0].Length];
                var dstd = new double[deltas[0].Length];
                var dvar = new double[deltas[0].Length];
                var dx = new Batch<double[]>(new double[deltas.Size][]);

                for (int i = 0; i < deltas[0].Length; i++)
                {
                    dbetaVector[i] = 0;
                    dgammaVector[i] = 0;
                    dstd[i] = 0;
                    dvar[i] = 0;
                }

                for (int i = 0; i < deltas[0].Length; i++)
                {
                    double sum = 0;

                    for (int j = 0; j < deltas.Size; j++)
                    {
                        dbetaVector[i] += deltas[j][i];
                        dgammaVector[i] += this.xn[j, i] * deltas[j][i];
                        dxn[j, i] = this.gamma[i] * deltas[j][i];
                        dxc[j, i] = dxn[j, i] / this.standardDeviations[i];
                        dstd[i] -= dxn[j, i] * this.xc[j, i] / (this.standardDeviations[i] * this.standardDeviations[i]);
                    }

                    dvar[i] = 0.5 * dstd[i] / this.standardDeviations[i];

                    for (int j = 0; j < deltas.Size; j++)
                    {
                        dxc[j, i] += (2.0 / deltas.Size) * this.xc[j, i] * dvar[i];
                        sum += dxc[j, i];
                    }

                    for (int j = 0; j < deltas.Size; j++)
                    {
                        dx[j][i] = dxc[j, i] - sum / deltas.Size;
                    }
                }

                return Tuple.Create<Batch<double[]>, Batch<double[]>>(dx, new Batch<double[]>(new double[1][] { dgammaVector.Concat<double>(dbetaVector).ToArray<double>() }));
            }

            public override void Update(Batch<double[]> gradients, Func<double, double, double> func)
            {
                var length = this.inputs * this.outputs;

                for (int i = 0; i < gradients.Size; i++)
                {
                    for (int j = 0; j < this.outputs; j++)
                    {
                        this.gamma[j] = func(this.gamma[j], gradients[i][j]);
                    }

                    for (int j = 0, k = this.outputs; j < this.outputs; j++, k++)
                    {
                        this.beta[j] = func(this.beta[j], gradients[i][k]);
                    }
                }
            }
        }
    }
}
